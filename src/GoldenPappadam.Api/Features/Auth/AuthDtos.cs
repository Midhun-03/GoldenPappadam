using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

public record CurrentUserDto(Guid Id, string Email, string FullName);

public record UserDto(Guid Id, string Email, string FullName, bool IsActive);

public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MaxLength(150)] string FullName,
    [Required, MinLength(8)] string Password);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);
