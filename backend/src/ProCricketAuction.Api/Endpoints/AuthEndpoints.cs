using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ProCricketAuction.Api.DTOs;
using ProCricketAuction.Api.Services;

namespace ProCricketAuction.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/v1/auth");

        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new CsrfTokenResponse(antiforgery.GetAndStoreTokens(context).RequestToken!)))
            .AllowAnonymous()
            .WithName("GetAuthCsrfToken")
            .WithSummary("Issue the CSRF token required by authentication actions")
            .Produces<CsrfTokenResponse>();

        auth.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("RegisterOrganizer")
            .WithSummary("Create an organizer account and start a secure session")
            .Produces<AuthUserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting("auth")
            .WithName("Login")
            .WithSummary("Sign in with an email and password")
            .Produces<AuthUserResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        auth.MapPost("/logout", LogoutAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth")
            .WithName("Logout")
            .WithSummary("End the current secure session")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        auth.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Return the signed-in account")
            .Produces<AuthUserResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

        auth.MapPut("/profile", UpdateProfileAsync)
            .RequireAuthorization()
            .WithName("UpdateProfile")
            .WithSummary("Update the signed-in user's display name")
            .Produces<AuthUserResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        auth.MapPost("/change-password", ChangePasswordAsync)
            .RequireAuthorization()
            .RequireRateLimiting("auth")
            .WithName("ChangePassword")
            .WithSummary("Change the signed-in user's password")
            .Produces<AuthUserResponse>()
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        return endpoints;
    }

    private static async Task<Results<Created<AuthUserResponse>, ValidationProblem>> RegisterAsync(
        RegisterRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["csrf"] = ["Refresh the page and try again."]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await authService.RegisterAsync(request);
        if (!result.Succeeded || result.User is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["account"] = result.Errors.ToArray()
            });
        }

        return TypedResults.Created("/api/v1/auth/me", result.User);
    }

    private static async Task<Results<Ok<AuthUserResponse>, ProblemHttpResult, ValidationProblem>> LoginAsync(
        LoginRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["csrf"] = ["Refresh the page and try again."]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await authService.LoginAsync(request);
        if (!result.Succeeded || result.User is null)
        {
            return TypedResults.Problem(
                detail: result.Errors.FirstOrDefault(),
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sign-in failed");
        }

        return TypedResults.Ok(result.User);
    }

    private static async Task<Results<NoContent, ValidationProblem>> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["csrf"] = ["Refresh the page and try again."]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        await authService.LogoutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AuthUserResponse>, UnauthorizedHttpResult>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await authService.GetCurrentUserAsync(principal);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(user);
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["csrf"] = ["Refresh the page and try again."]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await authService.UpdateProfileAsync(principal, request);
        return result.Succeeded && result.User is not null
            ? TypedResults.Ok(result.User)
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["profile"] = result.Errors.ToArray() });
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        HttpContext context,
        IAntiforgery antiforgery,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["csrf"] = ["Refresh the page and try again."]
            });
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await authService.ChangePasswordAsync(principal, request);
        return result.Succeeded && result.User is not null
            ? TypedResults.Ok(result.User)
            : TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["password"] = result.Errors.ToArray() });
    }
}
