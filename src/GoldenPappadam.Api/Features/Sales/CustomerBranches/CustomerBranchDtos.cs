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
    bool IsActive);

public record SaveCustomerBranchRequest(
    [Required, MaxLength(150)] string Name,
    [MaxLength(100)] string? Location,
    [MaxLength(300)] string? Address,
    [MaxLength(20)] string? Phone,
    [MaxLength(100)] string? ContactPerson);
