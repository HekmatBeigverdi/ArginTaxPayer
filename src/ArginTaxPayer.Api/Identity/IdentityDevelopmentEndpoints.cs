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
            IdentityProtectionService protection,
            CancellationToken cancellationToken) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            if (protection.IsBlocked(request.Email, DateTime.UtcNow))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            var valid = await identity.VerifyCredentialsAsync(request.Email, request.Password, cancellationToken);
            if (valid) protection.RecordSuccess(request.Email, "password_verify");
            else protection.RecordFailure(request.Email, DateTime.UtcNow, "password_verify");
            return valid
                ? Results.Ok(new { status = "credentials_verified", sessionIssued = false })
                : Results.Unauthorized();
        });

        // No tokens are issued for administrative accounts until MFA is implemented.
        group.MapPost("/login", async (
            [FromBody] IdentityCredentials request,
            HttpContext http,
            IdentitySessionService sessions,
            IdentityProtectionService protection,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            if (protection.IsBlocked(request.Email, DateTime.UtcNow))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            var tokens = await sessions.LoginAsync(request.Email, request.Password, ct);
            if (tokens is not null) protection.RecordSuccess(request.Email, "login");
            else protection.RecordFailure(request.Email, DateTime.UtcNow, "login");
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

        // MFA endpoints are restricted to Development and loopback, matching session routes.
        group.MapPost("/mfa/enroll", async (HttpContext http, IdentitySessionService sessions,
            IServiceProvider provider, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var identity = provider.GetService<IdentityMfaService>();
            if (identity is null) return Results.NotFound();
            var userId = await sessions.AuthenticateAsync(ReadBearer(http), ct);
            if (userId is null) return Results.Unauthorized();
            var enrollment = await identity.BeginEnrollmentAsync(userId.Value, ct);
            return enrollment is null ? Results.BadRequest(new { error = "mfa_unavailable" }) : Results.Ok(enrollment);
        });

        group.MapPost("/mfa/confirm", async ([FromBody] TotpConfirmRequest request,
            HttpContext http, IdentitySessionService sessions, IServiceProvider provider, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var identity = provider.GetService<IdentityMfaService>();
            if (identity is null) return Results.NotFound();
            var userId = await sessions.AuthenticateAsync(ReadBearer(http), ct);
            if (userId is null) return Results.Unauthorized();
            var codes = await identity.ConfirmEnrollmentAsync(userId.Value, request.Code, ct);
            return codes is null ? Results.Unauthorized() : Results.Ok(codes);
        });

        group.MapPost("/mfa/challenge", async ([FromBody] IdentityCredentials request,
            HttpContext http, IServiceProvider provider,
            IdentityProtectionService protection, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            if (protection.IsBlocked(request.Email, DateTime.UtcNow))
                return Results.StatusCode(StatusCodes.Status429TooManyRequests);
            var identity = provider.GetService<IdentityMfaService>();
            if (identity is null) return Results.NotFound();
            var challenge = await identity.BeginLoginAsync(request.Email, request.Password, ct);
            if (challenge is not null) protection.RecordSuccess(request.Email, "mfa_challenge");
            else protection.RecordFailure(request.Email, DateTime.UtcNow, "mfa_challenge");
            return challenge is null ? Results.Unauthorized() : Results.Ok(challenge);
        });

        group.MapPost("/mfa/complete", async ([FromBody] MfaCompleteRequest request,
            HttpContext http, IServiceProvider provider, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var identity = provider.GetService<IdentityMfaService>();
            if (identity is null) return Results.NotFound();
            var tokens = await identity.CompleteLoginAsync(request.Challenge, request.Code,
                request.UseRecoveryCode, ct);
            return tokens is null ? Results.Unauthorized() : Results.Ok(tokens);
        });

        // Step 05: all role decisions are made from the persisted identity,
        // never from claimed roles, user IDs, or permissions in the request.
        group.MapGet("/authorization/me", async (
            HttpContext http, IdentityAuthorizationService authorization, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var bearer = ReadBearer(http);
            var userId = await authorization.CurrentUserIdAsync(bearer, ct);
            if (userId is null) return Results.Unauthorized();
            var roles = await authorization.GetCurrentRolesAsync(bearer, ct);
            return Results.Ok(new { userId, roles });
        });

        group.MapGet("/authorization/admin", async (
            HttpContext http, IdentityAuthorizationService authorization, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var result = await authorization.CheckAsync(ReadBearer(http),
                IdentityPermissions.ReadAdministration, ct);
            return result switch
            {
                IdentityAuthorizationResult.Unauthenticated => Results.Unauthorized(),
                IdentityAuthorizationResult.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.Ok(new { area = "administration", access = "granted" })
            };
        });

        group.MapGet("/authorization/customer", async (
            HttpContext http, IdentityAuthorizationService authorization, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var result = await authorization.CheckAsync(ReadBearer(http),
                IdentityPermissions.ReadCustomerArea, ct);
            return result switch
            {
                IdentityAuthorizationResult.Unauthenticated => Results.Unauthorized(),
                IdentityAuthorizationResult.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => Results.Ok(new { area = "customer", access = "granted" })
            };
        });

        group.MapPost("/roles/level2/grant", async (
            [FromBody] Level2GrantRequest request, HttpContext http,
            IdentityAuthorizationService authorization, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            if (request.TargetUserId == Guid.Empty)
                return Results.BadRequest(new { error = "invalid_target" });
            var result = await authorization.GrantLevel2Async(
                ReadBearer(http), request.TargetUserId, ct);
            return result switch
            {
                Level2RoleGrantResult.Unauthenticated => Results.Unauthorized(),
                Level2RoleGrantResult.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                Level2RoleGrantResult.TargetNotFound => Results.NotFound(),
                Level2RoleGrantResult.Conflict => Results.Conflict(new { error = "role_already_assigned" }),
                _ => Results.Ok(new { status = "level2_granted", targetUserId = request.TargetUserId })
            };
        });

        // Step 06: ownership is derived from PostgreSQL by resource ID and
        // authenticated actor; no owner/tenant claim is accepted from a client.
        group.MapGet("/resources/users/{userId:guid}", async (
            Guid userId, HttpContext http, IdentityResourceAuthorizationService scopes,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var decision = await scopes.CheckAsync(ReadBearer(http),
                IdentityPermissions.ReadOwnIdentity, IdentityResourceKind.IdentityUser, userId, ct);
            return ScopeResult(decision, new { userId });
        });

        group.MapGet("/resources/sessions", async (
            HttpContext http, IdentityResourceAuthorizationService scopes,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var ids = await scopes.ListOwnSessionIdsAsync(ReadBearer(http), ct);
            return ids is null ? Results.Unauthorized() : Results.Ok(new { sessionIds = ids });
        });

        group.MapGet("/resources/sessions/{sessionId:guid}", async (
            Guid sessionId, HttpContext http, IdentityResourceAuthorizationService scopes,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var decision = await scopes.CheckAsync(ReadBearer(http),
                IdentityPermissions.ReadOwnIdentity,
                IdentityResourceKind.IdentitySession, sessionId, ct);
            return ScopeResult(decision, new { sessionId });
        });

        group.MapPost("/resources/sessions/{sessionId:guid}/revoke", async (
            Guid sessionId, HttpContext http, IdentityResourceAuthorizationService scopes,
            CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var decision = await scopes.RevokeOwnSessionAsync(ReadBearer(http), sessionId, ct);
            return decision switch
            {
                ResourceAccessDecision.Unauthenticated => Results.Unauthorized(),
                ResourceAccessDecision.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                ResourceAccessDecision.NotFound => Results.NotFound(),
                _ => Results.NoContent()
            };
        });

        return endpoints;
    }

    private static IResult ScopeResult(ResourceAccessDecision decision, object payload) => decision switch
    {
        ResourceAccessDecision.Unauthenticated => Results.Unauthorized(),
        ResourceAccessDecision.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
        ResourceAccessDecision.NotFound => Results.NotFound(),
        _ => Results.Ok(payload)
    };

    private static string? ReadBearer(HttpContext http)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim() : null;
    }

    internal static bool IsLoopbackAddress(IPAddress? address) =>
        address is { } ip &&
        (IPAddress.IsLoopback(ip) ||
            (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())));

    private static bool IsLoopback(HttpContext http) =>
        IsLoopbackAddress(http.Connection.RemoteIpAddress);
}

public sealed record IdentityCredentials(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record TotpConfirmRequest(string? Code);
public sealed record MfaCompleteRequest(string? Challenge, string? Code, bool UseRecoveryCode);

public sealed record Level2GrantRequest(Guid TargetUserId);
