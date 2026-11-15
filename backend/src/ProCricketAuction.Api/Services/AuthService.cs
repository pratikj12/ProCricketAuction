using Microsoft.AspNetCore.Identity;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Domain.Entities;

namespace ProCricketAuction.Api.Services;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole<Guid>> roleManager) : IAuthService
{
    public async Task<AuthServiceResult> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return new AuthServiceResult(false, null, createResult.Errors.Select(error => error.Description).ToArray());
        }

        const string organizerRole = "Organizer";
        if (!await roleManager.RoleExistsAsync(organizerRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(organizerRole));
            if (!roleResult.Succeeded)
            {
                await userManager.DeleteAsync(user);
                return new AuthServiceResult(false, null, ["Unable to create the organizer account. Please try again."]);
            }
        }

        var addRoleResult = await userManager.AddToRoleAsync(user, organizerRole);
        if (!addRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return new AuthServiceResult(false, null, ["Unable to create the organizer account. Please try again."]);
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return new AuthServiceResult(true, await ToResponseAsync(user), []);
    }

    public async Task<AuthServiceResult> LoginAsync(LoginRequest request)
    {
        var result = await signInManager.PasswordSignInAsync(
            request.Email.Trim(),
            request.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return new AuthServiceResult(false, null, ["Email or password is incorrect, or the account is temporarily locked."]);
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return new AuthServiceResult(false, null, ["Email or password is incorrect, or the account is temporarily locked."]);
        }

        return new AuthServiceResult(true, await ToResponseAsync(user), []);
    }

    public async Task<AuthUserResponse?> GetCurrentUserAsync(System.Security.Claims.ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        return user is null ? null : await ToResponseAsync(user);
    }

    public async Task<AuthServiceResult> UpdateProfileAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        UpdateProfileRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new AuthServiceResult(false, null, ["Your session has expired. Sign in again."]);
        }

        user.DisplayName = request.DisplayName.Trim();
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? new AuthServiceResult(true, await ToResponseAsync(user), [])
            : new AuthServiceResult(false, null, result.Errors.Select(error => error.Description).ToArray());
    }

    public async Task<AuthServiceResult> ChangePasswordAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return new AuthServiceResult(false, null, ["Your session has expired. Sign in again."]);
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return new AuthServiceResult(false, null, result.Errors.Select(error => error.Description).ToArray());
        }

        await signInManager.RefreshSignInAsync(user);
        return new AuthServiceResult(true, await ToResponseAsync(user), []);
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();

    private async Task<AuthUserResponse> ToResponseAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return new AuthUserResponse(user.Id, user.DisplayName, user.Email ?? string.Empty, roles.ToArray());
    }
}
