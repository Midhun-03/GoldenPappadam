using GoldenPappadam.Api.Common;
using GoldenPappadam.Domain.Inventory;
using GoldenPappadam.Domain.Sales;
using GoldenPappadam.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GoldenPappadam.Api.Features.Inventory.Products;

/// <summary>
/// Product rules that the database cannot express on its own: unique codes, references that
/// must exist and be active, packing chains that must not loop, and the own shop's pieces products
/// (one per loose variety, counted in pieces, with a rate band).
/// </summary>
public class ProductService(AppDbContext db)
{
    /// <summary>A pack made from a pack made from a pack... in practice one or two levels.</summary>
    private const int MaxSourceChainDepth = 10;

    public async Task<Product> CreateAsync(SaveProductRequest request, CancellationToken ct)
    {
        await EnsureCodeIsFree(request.ProductCode, null, ct);
        await EnsureCategoryAndUnitExist(request.CategoryId, request.UnitOfMeasureId, ct);

        // A brand new product cannot be part of a cycle, because nothing can point at it yet.
        var (sourceProductId, sourceQuantityPerPack, piecesPerPack) = await ResolveSourceAsync(Guid.Empty, request, ct);
        var piecesPerKg = await ResolvePiecesPerKgAsync(null, request, ct);
        var minimumSellingPrice = ResolveMinimumSellingPrice(request);
        var tax = ResolveTax(request);
        await EnsureTreatmentWhileGstIsOnAsync(tax.Treatment, ct);

        if (request.Kind == ProductKind.Pieces)
        {
            await EnsureOnePiecesProductPerVarietyAsync(null, sourceProductId, ct);
        }

        var product = new Product
        {
            ProductCode = request.ProductCode.Trim(),
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            Kind = request.Kind,
            UnitOfMeasureId = request.UnitOfMeasureId,
            SellingPrice = request.SellingPrice,
            MinimumSellingPrice = minimumSellingPrice,
            LowStockThreshold = request.LowStockThreshold,
            SourceProductId = sourceProductId,
            SourceQuantityPerPack = sourceQuantityPerPack,
            PiecesPerPack = piecesPerPack,
            PiecesPerKg = piecesPerKg,
            HsnCode = tax.HsnCode,
            TaxTreatment = tax.Treatment,
            GstRate = tax.GstRate,
            ShelfLifeDays = request.ShelfLifeDays
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);

        return product;
    }

    public async Task<Product> UpdateAsync(Guid id, SaveProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Product");

        if (product.Kind != request.Kind)
        {
            throw new DomainException(
                "A product cannot change between loose, packed and shop pieces. Create a new product instead.");
        }

        await EnsureCodeIsFree(request.ProductCode, id, ct);
        await EnsureCategoryAndUnitExist(request.CategoryId, request.UnitOfMeasureId, ct);

        if (product.UnitOfMeasureId != request.UnitOfMeasureId &&
            await db.StockMovements.AnyAsync(m => m.ProductId == id, ct))
        {
            throw new DomainException(
                "The unit cannot be changed once stock has moved, because past quantities were recorded in the old unit.");
        }

        // Everything is validated before the entity is touched, so a rejected request
        // never leaves a half-changed product behind in the change tracker.
        var (sourceProductId, sourceQuantityPerPack, piecesPerPack) = await ResolveSourceAsync(product.Id, request, ct);
        await EnsureNoCycleAsync(product.Id, sourceProductId, ct);
        var piecesPerKg = await ResolvePiecesPerKgAsync(product.Id, request, ct);
        var minimumSellingPrice = ResolveMinimumSellingPrice(request);
        var tax = ResolveTax(request);
        await EnsureTreatmentWhileGstIsOnAsync(tax.Treatment, ct);

        if (product.IsActive && product.Kind == ProductKind.Pieces)
        {
            await EnsureOnePiecesProductPerVarietyAsync(product.Id, sourceProductId, ct);
        }

        product.ProductCode = request.ProductCode.Trim();
        product.Name = request.Name.Trim();
        product.CategoryId = request.CategoryId;
        product.UnitOfMeasureId = request.UnitOfMeasureId;
        // A new rate band applies from the next shop sale; sales already made keep the rates they recorded.
        product.SellingPrice = request.SellingPrice;
        product.MinimumSellingPrice = minimumSellingPrice;
        product.LowStockThreshold = request.LowStockThreshold;
        product.SourceProductId = sourceProductId;
        product.SourceQuantityPerPack = sourceQuantityPerPack;
        product.PiecesPerPack = piecesPerPack;

        // A new figure applies to packing from now on; packing entries already made keep the one they used.
        product.PiecesPerKg = piecesPerKg;

        // A new rate applies from the next bill. Invoices already made keep the tax they printed.
        product.HsnCode = tax.HsnCode;
        product.TaxTreatment = tax.Treatment;
        product.GstRate = tax.GstRate;

        // A new shelf life changes how old stock is judged from now on; nothing already recorded moves.
        product.ShelfLifeDays = request.ShelfLifeDays;

        await db.SaveChangesAsync(ct);

        return product;
    }

    public async Task<Product> SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                      ?? throw new NotFoundException("Product");

        if (!isActive && await db.Products.AnyAsync(p => p.SourceProductId == id && p.IsActive, ct))
        {
            throw new DomainException(
                "Active products are still made from this product - packets packed from it, or the pieces the own " +
                "shop sells of it. Deactivate those first.");
        }

        if (isActive && !product.IsActive && product.Kind == ProductKind.Pieces)
        {
            await EnsureOnePiecesProductPerVarietyAsync(product.Id, product.SourceProductId, ct);
        }

        product.IsActive = isActive;
        await db.SaveChangesAsync(ct);

        return product;
    }

    /// <summary>
    /// The tax fields as they will be stored. Which treatment and rate are right is the accountant's
    /// decision; this only makes sure the three fields make sense together.
    /// </summary>
    private static (string? HsnCode, TaxTreatment? Treatment, decimal? GstRate) ResolveTax(SaveProductRequest request)
    {
        var hsn = string.IsNullOrWhiteSpace(request.HsnCode) ? null : request.HsnCode.Trim();

        if (hsn is not null && !(hsn.Length is 4 or 6 or 8 && hsn.All(char.IsAsciiDigit)))
        {
            throw new DomainException("An HSN code is 4, 6 or 8 digits.");
        }

        if (request.TaxTreatment != TaxTreatment.Taxable)
        {
            return (hsn, request.TaxTreatment, null);
        }

        if (request.GstRate is not (> 0m and <= 100m))
        {
            throw new DomainException("A taxable product needs its GST rate, as a percentage above zero.");
        }

        return (hsn, TaxTreatment.Taxable, request.GstRate);
    }

    /// <summary>
    /// Once the business has a GSTIN, every bill needs to know how its products are taxed, so a
    /// product cannot be left - or put back - as "not decided".
    /// </summary>
    private async Task EnsureTreatmentWhileGstIsOnAsync(TaxTreatment? treatment, CancellationToken ct)
    {
        if (treatment is null && await db.InvoiceSettings.AnyAsync(s => s.Gstin != null, ct))
        {
            throw new DomainException(
                "Choose the GST treatment for this product. The business has a GSTIN, so every product needs one.");
        }
    }

    /// <summary>
    /// A loose variety counted in kg may carry its pieces per kg; nothing else may. Removing it from a
    /// variety that count-based packets are packed from would leave them impossible to pack, so it is
    /// refused.
    /// </summary>
    private async Task<decimal?> ResolvePiecesPerKgAsync(Guid? productId, SaveProductRequest request, CancellationToken ct)
    {
        if (request.PiecesPerKg is not null &&
            (request.Kind != ProductKind.Loose || request.UnitOfMeasureId != KnownUnits.KilogramId))
        {
            throw new DomainException("Pieces per kg belongs to a loose product counted in kg.");
        }

        if (request.PiecesPerKg is null && productId is { } id &&
            await db.Products.AnyAsync(
                p => p.SourceProductId == id && (p.PiecesPerPack != null || p.Kind == ProductKind.Pieces) && p.IsActive, ct))
        {
            throw new DomainException(
                "Packets counted in pieces, or the own shop's pieces, come from this pappadam, so it needs its pieces per kg.");
        }

        return request.PiecesPerKg;
    }

    /// <summary>Works out the source fields for a request without touching the entity.</summary>
    private async Task<(Guid? SourceProductId, decimal? SourceQuantityPerPack, int? PiecesPerPack)> ResolveSourceAsync(
        Guid productId,
        SaveProductRequest request,
        CancellationToken ct)
    {
        if (request.Kind == ProductKind.Pieces)
        {
            return (await ResolvePiecesSourceAsync(request, ct), null, null);
        }

        if (request.Kind != ProductKind.Packed)
        {
            return (null, null, null);
        }

        if (request.SourceProductId is null)
        {
            throw new DomainException("A packed product needs the product it is packed from.");
        }

        var byPieces = request.PiecesPerPack is > 0;
        var byQuantity = request.SourceQuantityPerPack is > 0;

        if (byPieces == byQuantity)
        {
            throw new DomainException(
                "Say how much one pack holds: a number of pieces, or a quantity of what it is packed from - one, not both.");
        }

        if (request.SourceProductId == productId)
        {
            throw new DomainException("A product cannot be packed from itself.");
        }

        var source = await db.Products
                         .AsNoTracking()
                         .FirstOrDefaultAsync(p => p.Id == request.SourceProductId, ct)
                     ?? throw new NotFoundException("Source product");

        if (!source.IsActive)
        {
            throw new DomainException($"The source product '{source.Name}' is not active.");
        }

        // The shop's pieces are sold loose by the piece; a bundle of them is still pieces, never a product.
        if (source.Kind == ProductKind.Pieces)
        {
            throw new DomainException(
                $"'{source.Name}' is the own shop's pieces, which are sold loose and never packed. " +
                "Pack from the loose pappadam instead.");
        }

        if (byPieces && (source.Kind != ProductKind.Loose || source.UnitOfMeasureId != KnownUnits.KilogramId))
        {
            throw new DomainException(
                "A packet is counted in pieces only when it is packed from loose pappadam counted in kg. " +
                "For a box of packets, give the number of packets it holds.");
        }

        return byPieces
            ? (source.Id, null, request.PiecesPerPack)
            : (source.Id, request.SourceQuantityPerPack, null);
    }

    /// <summary>
    /// An own-shop pieces product: counted in pieces, and made from a loose variety counted in kg that
    /// knows its pieces per kg - which is how a transfer turns the factory's kg into the shop's pieces.
    /// </summary>
    private async Task<Guid> ResolvePiecesSourceAsync(SaveProductRequest request, CancellationToken ct)
    {
        if (request.UnitOfMeasureId != KnownUnits.PieceId)
        {
            throw new DomainException("The own shop's pappadam is counted in pieces. Choose the PCS unit.");
        }

        if (request.SellingPrice is not > 0m)
        {
            throw new DomainException("Enter the shop's rate per piece.");
        }

        if (request.SourceProductId is not { } sourceId)
        {
            throw new DomainException("Choose the loose pappadam these pieces come from.");
        }

        var source = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == sourceId, ct)
                     ?? throw new NotFoundException("Source product");

        if (source.Kind != ProductKind.Loose || source.UnitOfMeasureId != KnownUnits.KilogramId)
        {
            throw new DomainException(
                $"'{source.Name}' is not loose pappadam counted in kg. The shop's pieces come from a loose variety.");
        }

        if (!source.IsActive)
        {
            throw new DomainException($"The source product '{source.Name}' is not active.");
        }

        if (source.PiecesPerKg is null)
        {
            throw new DomainException(
                $"'{source.Name}' has no pieces per kg, so its kg cannot be turned into pieces. Set it on that product first.");
        }

        return source.Id;
    }

    /// <summary>
    /// The own shop's rate band. The rate per piece is also the most a piece may be sold for (owner,
    /// 2026-09-30), so the minimum cannot be above it.
    /// </summary>
    private static decimal? ResolveMinimumSellingPrice(SaveProductRequest request)
    {
        if (request.MinimumSellingPrice is not { } minimum)
        {
            return null;
        }

        if (request.Kind != ProductKind.Pieces)
        {
            throw new DomainException("A minimum rate belongs to the own shop's pieces products only.");
        }

        if (minimum <= 0m)
        {
            throw new DomainException("The minimum rate must be more than zero.");
        }

        if (request.SellingPrice is { } rate && minimum > rate)
        {
            throw new DomainException(
                $"The minimum rate ({minimum:0.00}) cannot be above the rate per piece ({rate:0.00}).");
        }

        return minimum;
    }

    /// <summary>One active pieces product per loose variety, so a transfer never has to guess which.</summary>
    private async Task EnsureOnePiecesProductPerVarietyAsync(Guid? exceptId, Guid? sourceProductId, CancellationToken ct)
    {
        var other = await db.Products
            .Where(p => p.Kind == ProductKind.Pieces && p.IsActive && p.SourceProductId == sourceProductId)
            .Where(p => exceptId == null || p.Id != exceptId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);

        if (other is not null)
        {
            throw new DomainException(
                $"'{other}' already holds the own shop's pieces of that pappadam. One variety has one pieces product.");
        }
    }

    /// <summary>Walks up the packing chain so a product can never end up packed from itself.</summary>
    private async Task EnsureNoCycleAsync(Guid productId, Guid? sourceProductId, CancellationToken ct)
    {
        var currentId = sourceProductId;

        for (var depth = 0; currentId is not null; depth++)
        {
            if (currentId == productId)
            {
                throw new DomainException("This would make the product packed from itself through its source chain.");
            }

            if (depth >= MaxSourceChainDepth)
            {
                throw new DomainException("The packing chain is too long.");
            }

            var next = currentId;
            currentId = await db.Products
                .Where(p => p.Id == next)
                .Select(p => p.SourceProductId)
                .FirstOrDefaultAsync(ct);
        }
    }

    private async Task EnsureCodeIsFree(string productCode, Guid? exceptId, CancellationToken ct)
    {
        var code = productCode.Trim();
        var taken = await db.Products.AnyAsync(p => p.ProductCode == code && (exceptId == null || p.Id != exceptId), ct);

        if (taken)
        {
            throw new DomainException($"A product with code '{code}' already exists.");
        }
    }

    private async Task EnsureCategoryAndUnitExist(Guid categoryId, Guid unitId, CancellationToken ct)
    {
        if (!await db.ProductCategories.AnyAsync(c => c.Id == categoryId && c.IsActive, ct))
        {
            throw new DomainException("The category does not exist or is not active.");
        }

        if (!await db.UnitOfMeasures.AnyAsync(u => u.Id == unitId && u.IsActive, ct))
        {
            throw new DomainException("The unit does not exist or is not active.");
        }
    }
}
