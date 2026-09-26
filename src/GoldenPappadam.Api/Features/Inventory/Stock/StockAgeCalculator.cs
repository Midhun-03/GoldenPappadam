using GoldenPappadam.Domain.Inventory;

namespace GoldenPappadam.Api.Features.Inventory.Stock;

/// <summary>
/// How old every packet in stock is, worked out from the stock ledger alone - no batch numbers to
/// type in anywhere. Stock is taken first-in-first-out, the way the warehouse and the van actually
/// work: the oldest packets go first. Each place keeps a queue of "layers", each a quantity packed on
/// one day.
///
/// - Packing, production, opening stock and positive adjustments add a layer dated that day.
/// - Sales, damage and negative adjustments take the oldest layers first.
/// - Loading or emptying the van moves the oldest layers with their original packing date, so a
///   packet's age does not reset because it went for a ride.
/// - A cancelled bill puts back exactly the layers its sale took.
/// - Repacking takes the oldest layers of the packets opened; the new packets start a fresh life on
///   the repacking day, and any loose left-over keeps the age of the oldest packet it came from.
/// - Stock that went negative (allowed, with a warning) is a debt the next arrival pays first.
///
/// Pure arithmetic over the movements it is given, so every rule is tested without a database.
/// </summary>
public static class StockAgeCalculator
{
    public record Movement(
        Guid ProductId,
        Guid LocationId,
        StockMovementType Type,
        decimal Quantity,
        DateOnly Day,
        DateTime OccurredAt,
        DateTime CreatedAt,
        Guid? ReferenceId);

    public record Layer(DateOnly Day, decimal Quantity);

    /// <summary>What is left in each place, oldest layer first. Only places holding stock are returned.</summary>
    public static Dictionary<(Guid ProductId, Guid LocationId), List<Layer>> Compute(
        IEnumerable<Movement> movements,
        IReadOnlySet<Guid> looseProductIds)
    {
        var queues = new Dictionary<(Guid, Guid), LinkedList<Layer>>();
        var deficits = new Dictionary<(Guid, Guid), decimal>();

        // What the "out" half of a two-sided operation took, for its "in" half to carry on.
        var moved = new Dictionary<(Guid? Reference, Guid Product), Queue<Layer>>();
        var sold = new Dictionary<(Guid? Reference, Guid Product), Queue<Layer>>();
        var repackOldest = new Dictionary<Guid, DateOnly>();

        // Both halves of a transfer or a repack are saved together, so they share their timestamps;
        // taking the "out" half first means the "in" half always finds what it carries.
        var ordered = movements
            .OrderBy(m => m.OccurredAt)
            .ThenBy(m => m.CreatedAt)
            .ThenBy(m => m.Quantity < 0m ? 0 : 1);

        foreach (var m in ordered)
        {
            var key = (m.ProductId, m.LocationId);

            if (m.Quantity < 0m)
            {
                var taken = Take(key, -m.Quantity);

                switch (m.Type)
                {
                    case StockMovementType.Transfer:
                        Remember(moved, (m.ReferenceId, m.ProductId), taken);
                        break;
                    case StockMovementType.Sale:
                        Remember(sold, (m.ReferenceId, m.ProductId), taken);
                        break;
                    case StockMovementType.Repacking when m.ReferenceId is { } entry && taken.Count > 0:
                        var oldest = taken.Min(l => l.Day);
                        repackOldest[entry] = repackOldest.TryGetValue(entry, out var known) && known < oldest ? known : oldest;
                        break;
                }

                continue;
            }

            switch (m.Type)
            {
                case StockMovementType.Transfer:
                    Carry(key, m, moved);
                    break;

                case StockMovementType.SaleReversal:
                    Carry(key, m, sold);
                    break;

                // The loose left-over of a repack is as old as the packets it came out of; the new
                // packets are fresh.
                case StockMovementType.Repacking
                    when looseProductIds.Contains(m.ProductId) &&
                         m.ReferenceId is { } entry && repackOldest.TryGetValue(entry, out var day):
                    Add(key, new Layer(day, m.Quantity));
                    break;

                default:
                    Add(key, new Layer(m.Day, m.Quantity));
                    break;
            }
        }

        return queues
            .Where(q => q.Value.Sum(l => l.Quantity) > 0m)
            .ToDictionary(q => q.Key, q => q.Value.Where(l => l.Quantity > 0m).ToList());

        List<Layer> Take((Guid, Guid) key, decimal quantity)
        {
            var queue = Queue(key);
            var taken = new List<Layer>();

            while (quantity > 0m && queue.First is { } first)
            {
                var part = Math.Min(first.Value.Quantity, quantity);
                taken.Add(first.Value with { Quantity = part });
                quantity -= part;

                if (part == first.Value.Quantity)
                {
                    queue.RemoveFirst();
                }
                else
                {
                    first.Value = first.Value with { Quantity = first.Value.Quantity - part };
                }
            }

            if (quantity > 0m)
            {
                deficits[key] = deficits.GetValueOrDefault(key) + quantity;
            }

            return taken;
        }

        void Add((Guid, Guid) key, Layer layer)
        {
            // Stock that went below zero has already left; what arrives first fills that gap.
            var owed = deficits.GetValueOrDefault(key);
            if (owed > 0m)
            {
                var paid = Math.Min(owed, layer.Quantity);
                deficits[key] = owed - paid;
                layer = layer with { Quantity = layer.Quantity - paid };
            }

            if (layer.Quantity <= 0m)
            {
                return;
            }

            // Kept in date order, so "take the oldest" is always "take from the front".
            var queue = Queue(key);
            var node = queue.Last;
            while (node is not null && node.Value.Day > layer.Day)
            {
                node = node.Previous;
            }

            if (node is not null && node.Value.Day == layer.Day)
            {
                // One layer per packing day: stock coming back joins what was packed with it.
                node.Value = node.Value with { Quantity = node.Value.Quantity + layer.Quantity };
            }
            else if (node is null)
            {
                queue.AddFirst(layer);
            }
            else
            {
                queue.AddAfter(node, layer);
            }
        }

        void Carry((Guid, Guid) key, Movement m, Dictionary<(Guid?, Guid), Queue<Layer>> from)
        {
            var remaining = m.Quantity;

            if (from.TryGetValue((m.ReferenceId, m.ProductId), out var carried))
            {
                while (remaining > 0m && carried.Count > 0)
                {
                    var layer = carried.Dequeue();
                    var part = Math.Min(layer.Quantity, remaining);
                    Add(key, layer with { Quantity = part });
                    remaining -= part;

                    if (part < layer.Quantity)
                    {
                        // Put the rest back at the front for the next arrival.
                        var rest = new Queue<Layer>([layer with { Quantity = layer.Quantity - part }, .. carried]);
                        from[(m.ReferenceId, m.ProductId)] = carried = rest;
                    }
                }
            }

            // Nothing on record to carry (the stock had gone negative): all we know is today.
            if (remaining > 0m)
            {
                Add(key, new Layer(m.Day, remaining));
            }
        }

        LinkedList<Layer> Queue((Guid, Guid) key) =>
            queues.TryGetValue(key, out var queue) ? queue : queues[key] = new LinkedList<Layer>();
    }

    private static void Remember(
        Dictionary<(Guid?, Guid), Queue<Layer>> store,
        (Guid?, Guid) key,
        List<Layer> layers)
    {
        if (!store.TryGetValue(key, out var queue))
        {
            store[key] = queue = new Queue<Layer>();
        }

        foreach (var layer in layers)
        {
            queue.Enqueue(layer);
        }
    }
}

/// <summary>
/// The age bands, relative to a product's shelf life so a product that lasts 30 days gets bands
/// that make sense for it. For pappadam's 20 days: 0–4 fresh, 5–10 worth repacking, 11–15 ageing,
/// 16–20 expiring soon, 21 and over expired.
/// </summary>
public record AgeBands(int FreshUpTo, int RepackUpTo, int AgeingUpTo, int ShelfLife)
{
    /// <summary>"Expiring within 3 days" on the dashboard.</summary>
    public const int SoonDays = 3;

    public static AgeBands For(int shelfLife) => new(
        Math.Max((int)Math.Ceiling(shelfLife * 0.25m) - 1, 0),
        shelfLife / 2,
        (int)Math.Ceiling(shelfLife * 0.75m),
        shelfLife);

    public string FreshLabel => $"0–{FreshUpTo} days";
    public string RepackLabel => $"{FreshUpTo + 1}–{RepackUpTo} days";
    public string AgeingLabel => $"{RepackUpTo + 1}–{AgeingUpTo} days";
    public string ExpiringLabel => $"{AgeingUpTo + 1}–{ShelfLife} days";
}
