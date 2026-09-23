using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Sales.CustomerBranches;

public record CustomerBranchDto(
    Guid Id,
    Guid CustomerId,
    string Name,
    string? Location,
    string? Address,
    string? Phone,
    string? ContactPerson,
    bool IsActive,
    string? Gstin,
    string? StateCode);

public record SaveCustomerBranchRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? Location,
    [MaxLength(300)] string? Address,
    [MaxLength(20)] string? Phone,
    [MaxLength(100)] string? ContactPerson,
    [MaxLength(15)] string? Gstin = null,
    [StringLength(2, MinimumLength = 2)] string? StateCode = null);
