using System.ComponentModel.DataAnnotations;

namespace ProCricketAuction.Api.DTOs;

/// <summary>Credentials for creating an organizer account.</summary>
public sealed record RegisterRequest
{
    /// <summary>The name shown to other tournament members.</summary>
    [Required, StringLength(100, MinimumLength = 2)]
    public required string DisplayName { get; init; }

    /// <summary>The account email address.</summary>
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    /// <summary>A password with at least twelve characters, upper and lower case, a digit, and a symbol.</summary>
    [Required, MinLength(12), MaxLength(128)]
    public required string Password { get; init; }
}

/// <summary>Credentials for signing in to an existing account.</summary>
public sealed record LoginRequest
{
    /// <summary>The account email address.</summary>
    [Required, EmailAddress, StringLength(256)]
    public required string Email { get; init; }

    /// <summary>The account password.</summary>
    [Required, MaxLength(128)]
    public required string Password { get; init; }
}

public sealed record UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public required string DisplayName { get; init; }
}

public sealed record ChangePasswordRequest
{
    [Required, MaxLength(128)]
    public required string CurrentPassword { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Authenticated account details safe to return to the browser.</summary>
public sealed record AuthUserResponse(Guid Id, string DisplayName, string Email, IReadOnlyList<string> Roles);

/// <summary>A one-time request token used to protect cookie-authenticated requests from CSRF.</summary>
public sealed record CsrfTokenResponse(string RequestToken);
