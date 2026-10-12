using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace ArginTaxPayer.Api.Identity;

/// <summary>
/// Phase 03 Step 02: restricted development credential routes.
/// NOT an authenticated login or a production registration feature.
/// </summary>
public static class IdentityDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapIdentityDevelopmentEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dev/identity").WithTags("Identity (Development)");
        group.MapPost("/register", async (
            [FromBody] IdentityCredentials request,
            HttpContext http,
            IdentityCredentialService identity,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!IsLoopback(http) || !configuration.GetValue<bool>("Identity:AllowDevelopmentRegistration"))
                return Results.NotFound();

            var result = await identity.RegisterCustomerAsync(request.Email, request.Password, cancellationToken);
            return result switch
            {
                IdentityRegistrationResult.Created => Results.StatusCode(StatusCodes.Status201Created),
                IdentityRegistrationResult.Conflict => Results.Conflict(new { error = "account_unavailable" }),
                _ => Results.BadRequest(new { error = "invalid_registration" })
            };
        });

        group.MapPost("/verify-password", async (
            [FromBody] IdentityCredentials request,
            HttpContext http,
            IdentityCredentialService identity,
            CancellationToken cancellationToken) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var valid = await identity.VerifyCredentialsAsync(request.Email, request.Password, cancellationToken);
            return valid
                ? Results.Ok(new { status = "credentials_verified", sessionIssued = false })
                : Results.Unauthorized();
        });

        // No tokens are issued for administrative accounts until MFA is implemented.
        group.MapPost("/login", async (
            [FromBody] IdentityCredentials request,
            HttpContext http,
            IdentitySessionService sessions,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var tokens = await sessions.LoginAsync(request.Email, request.Password, ct);
            return tokens is null ? Results.Unauthorized() : Results.Ok(tokens);
        });

        group.MapPost("/refresh", async (
            [FromBody] RefreshRequest request,
            HttpContext http,
            IdentitySessionService sessions,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var tokens = await sessions.RefreshAsync(request.RefreshToken, ct);
            return tokens is null ? Results.Unauthorized() : Results.Ok(tokens);
        });

        group.MapPost("/logout", async (
            HttpContext http,
            IdentitySessionService sessions,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var token = ReadBearer(http);
            return await sessions.LogoutAsync(token, ct)
                ? Results.NoContent() : Results.Unauthorized();
        });

        group.MapGet("/me", async (
            HttpContext http,
            IdentitySessionService sessions,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var userId = await sessions.AuthenticateAsync(ReadBearer(http), ct);
            return userId is { } id ? Results.Ok(new { userId = id }) : Results.Unauthorized();
        });

        return endpoints;
    }

    private static string? ReadBearer(HttpContext http)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim() : null;
    }

    private static bool IsLoopback(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } ip && IPAddress.IsLoopback(ip);
}

public sealed record IdentityCredentials(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);
