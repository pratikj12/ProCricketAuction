using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using ProCricketAuction.Domain.Entities;

namespace ProCricketAuction.Api.Security;

public sealed class SuperAdministratorRequirement : IAuthorizationRequirement
{
}

public sealed class SuperAdministratorAuthorizationHandler(
    UserManager<ApplicationUser> userManager) : AuthorizationHandler<SuperAdministratorRequirement>
{
    public const string DesignatedEmail = "jadhavpratikraje1998@gmail.com";
    public const string PolicyName = "SuperAdministrator";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SuperAdministratorRequirement requirement)
    {
        var user = await userManager.GetUserAsync(context.User);
        if (user is null || !user.EmailConfirmed)
        {
            return;
        }

        var expectedEmail = userManager.NormalizeEmail(DesignatedEmail);
        var accountEmail = userManager.NormalizeEmail(user.Email);
        if (string.Equals(accountEmail, expectedEmail, StringComparison.Ordinal)
            && await userManager.IsInRoleAsync(user, "SuperAdministrator"))
        {
            context.Succeed(requirement);
        }
    }
}
