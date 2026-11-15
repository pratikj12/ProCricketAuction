using ProCricketAuction.Api.DTOs;

namespace ProCricketAuction.Api.Services;

public interface IAuthService
{
    Task<AuthServiceResult> RegisterAsync(RegisterRequest request);
    Task<AuthServiceResult> LoginAsync(LoginRequest request);
    Task<AuthUserResponse?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal);
    Task<AuthServiceResult> UpdateProfileAsync(System.Security.Claims.ClaimsPrincipal principal, UpdateProfileRequest request);
    Task<AuthServiceResult> ChangePasswordAsync(System.Security.Claims.ClaimsPrincipal principal, ChangePasswordRequest request);
    Task LogoutAsync();
}

public sealed record AuthServiceResult(
    bool Succeeded,
    AuthUserResponse? User,
    IReadOnlyList<string> Errors);
