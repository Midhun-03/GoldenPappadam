using Microsoft.AspNetCore.Mvc;

namespace GoldenPappadam.Api.Features.Sales.Payments;

[ApiController]
[Route("api/sales/payments")]
public class PaymentsController(PaymentService payments) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PaymentDto>> GetAll(
        Guid? customerId = null,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken ct = default) =>
        payments.GetAllAsync(customerId, from, to, ct);

    [HttpGet("{id:guid}")]
    public Task<PaymentDto> GetById(Guid id, CancellationToken ct) => payments.GetAsync(id, ct);

    /// <summary>Records money received and applies it to bills, oldest first unless told otherwise.</summary>
    [HttpPost]
    public Task<CreatePaymentResponse> Create(CreatePaymentRequest request, CancellationToken ct) =>
        payments.CreateAsync(request, ct);
}
