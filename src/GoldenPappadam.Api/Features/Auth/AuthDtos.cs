using System.ComponentModel.DataAnnotations;

namespace GoldenPappadam.Api.Features.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password,
    bool RememberMe = false);

/// <summary>The refresh token from a previous mobile login or refresh.</summary>
public record RefreshRequest([Required] string RefreshToken);

public record CurrentUserDto(Guid Id, string Email, string FullName, string Role);

public record UserDto(Guid Id, string Email, string FullName, string Role, bool IsActive);

public record CreateUserRequest(
    [Required, EmailAddress] string Email,
    [Required, MaxLength(150)] string FullName,
    [Required, MinLength(8)] string Password,
    [Required] string Role);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);
