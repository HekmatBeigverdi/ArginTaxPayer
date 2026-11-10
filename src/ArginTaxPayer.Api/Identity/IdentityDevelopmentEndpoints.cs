using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
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
        group.MapGet("/mfa/status", async (HttpContext http, IdentityAuthorizationService authorization,
            ArginTaxPayerDbContext db, IServiceProvider provider, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var userId = await authorization.CurrentUserIdAsync(ReadBearer(http), ct);
            if (userId is null) return Results.Unauthorized();
            var roles = await authorization.GetCurrentRolesAsync(ReadBearer(http), ct);
            if (roles is null || !roles.Any(IdentityRoleNames.CanEnrollMfa)) return Results.StatusCode(403);
            var enabled = await db.IdentityMfaStates.AnyAsync(m => m.UserId == userId && m.EnabledAtUtc != null, ct);
            return Results.Ok(new { enabled, available = provider.GetService<IdentityMfaService>() is not null });
        });
        group.MapPost("/mfa/enroll", async (HttpContext http, IdentitySessionService sessions,
            IdentityAuthorizationService authorization, IServiceProvider provider, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var userId = await sessions.AuthenticateAsync(ReadBearer(http), ct);
            if (userId is null) return Results.Unauthorized();
            var roles = await authorization.GetCurrentRolesAsync(ReadBearer(http), ct);
            if (roles is null || !roles.Any(IdentityRoleNames.CanEnrollMfa)) return Results.StatusCode(403);
            var identity = provider.GetService<IdentityMfaService>();
            if (identity is null) return Results.Json(new { error = "mfa_not_configured" }, statusCode: 503);
            var enrollment = await identity.BeginEnrollmentForSessionAsync(ReadBearer(http), ct);
            return enrollment is null ? Results.BadRequest(new { error = "mfa_unavailable" }) : Results.Ok(enrollment);
        });

        group.MapPost("/mfa/reveal", async ([FromBody] MfaRevealRequest request, HttpContext http,
            IdentityAuthorizationService authorization, IServiceProvider provider,
            IdentityProtectionService protection, ArginTaxPayerDbContext db,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var token = ReadBearer(http);
            var userId = await authorization.CurrentUserIdAsync(token, ct);
            if (userId is null) return Results.Unauthorized();
            var roles = await authorization.GetCurrentRolesAsync(token, ct);
            if (roles is null || !roles.Any(role => role is IdentityRoleNames.Level1 or IdentityRoleNames.Level2))
                return Results.StatusCode(403);
            if (request.Code is not { Length: 6 } || request.Code.Any(ch => ch is < '0' or > '9'))
                return Results.BadRequest(new { error = "invalid_code" });
            var service = provider.GetService<IdentityMfaService>();
            if (service is null) return Results.Json(new { error = "mfa_not_configured" }, statusCode: 503);

            if (!await db.IdentityUsers.AsNoTracking().AnyAsync(user =>
                user.Id == userId.Value && user.IsActive, ct)) return Results.Unauthorized();
            var protectionKey = "mfa-secret-reveal:" + userId.Value.ToString("N");
            var now = DateTime.UtcNow;
            if (protection.IsBlocked(protectionKey, now)) return Results.StatusCode(429);
            var enrollment = await service.RevealEnabledSecretForSessionAsync(token, request.Code, ct);
            if (enrollment is null)
            {
                protection.RecordFailure(protectionKey, now, "mfa_secret_reveal");
                return Results.Unauthorized();
            }
            protection.RecordSuccess(protectionKey, "mfa_secret_reveal");
            loggerFactory.CreateLogger("ArginTaxPayer.Identity.MfaSecretReveal")
                .LogWarning("IDENTITY_MFA_SECRET_REVEAL user={UserId}", userId.Value);
            return Results.Ok(enrollment);
        }).RequireRateLimiting("signing-import");

        group.MapPost("/mfa/confirm", async ([FromBody] TotpConfirmRequest request,
            HttpContext http, IdentitySessionService sessions, IdentityAuthorizationService authorization,
            IServiceProvider provider, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var userId = await sessions.AuthenticateAsync(ReadBearer(http), ct);
            if (userId is null) return Results.Unauthorized();
            var roles = await authorization.GetCurrentRolesAsync(ReadBearer(http), ct);
            if (roles is null || !roles.Any(IdentityRoleNames.CanEnrollMfa)) return Results.StatusCode(403);
            var identity = provider.GetService<IdentityMfaService>();
            if (identity is null) return Results.Json(new { error = "mfa_not_configured" }, statusCode: 503);
            var codes = await identity.ConfirmEnrollmentAsync(userId.Value, request.Code, ct);
            return codes is null ? Results.Unauthorized() : Results.Ok(codes);
        });

        group.MapPost("/mfa/level2/reset", async ([FromBody] Level2MfaResetRequest request,
            HttpContext http, IdentityLevel2MfaResetService service, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.ResetAsync(ReadBearer(http), request.TargetUserId, ct);
            return result switch
            {
                Level2MfaResetResult.Unauthenticated => Results.Unauthorized(),
                Level2MfaResetResult.Forbidden => Results.StatusCode(403),
                Level2MfaResetResult.NotFound => Results.NotFound(),
                Level2MfaResetResult.Conflict => Results.Conflict(new { error = "mfa_reset_conflict" }),
                _ => Results.Ok(new { status = "mfa_reset", enrollmentRequired = true })
            };
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

        // Step 09: mobile ownership proof and passwordless customer login.
        // Routes remain Development/loopback only until the production identity gate.
        group.MapPost("/phone/request", async (
            [FromBody] PhoneRequest request, HttpContext http, IdentityPhoneService phones,
            ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            // Explicit errors are restricted to Development + loopback; a public
            // production identity API must not reveal registration existence.
            var logger = loggerFactory.CreateLogger("ArginTaxPayer.Identity.OtpDelivery");
            PhoneOtpRequestResult result;
            try
            {
                result = await phones.RequestDetailedAsync(request.Mobile, request.Purpose, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                // Keep the public error generic and never log request data, provider
                // bodies, credentials, or OTP values. The exception type is enough
                // to locate failures in the multi-step SMS dispatch pipeline.
                logger.LogError("OTP_REQUEST_PIPELINE_EXCEPTION exception_type={ExceptionType}",
                    exception.GetType().FullName);
                return Results.Json(new { error = "otp_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            if (result.Status == PhoneOtpRequestStatus.Sent)
            {
                logger.LogInformation("OTP_REQUEST_DELIVERED");
                return Results.Accepted();
            }
            logger.LogWarning("OTP_REQUEST_NOT_DELIVERED reason={OtpRequestStatus}", result.Status);
            return result.Status switch
            {
                PhoneOtpRequestStatus.AlreadyRegistered =>
                    Results.Conflict(new { error = "mobile_already_registered" }),
                PhoneOtpRequestStatus.NotRegistered =>
                    Results.NotFound(new { error = "mobile_not_registered" }),
                PhoneOtpRequestStatus.RateLimited =>
                    Results.Json(new { error = "otp_cooldown", retryAfterSeconds = result.RetryAfterSeconds },
                        statusCode: StatusCodes.Status429TooManyRequests),
                PhoneOtpRequestStatus.Invalid =>
                    Results.BadRequest(new { error = "invalid_phone_request" }),
                PhoneOtpRequestStatus.InsufficientCredit =>
                    Results.Json(new { error = "insufficient_sms_credit" },
                        statusCode: StatusCodes.Status402PaymentRequired),
                _ => Results.Json(new { error = "otp_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable)
            };
        });

        group.MapPost("/phone/register/complete", async (
            [FromBody] PhoneRegisterComplete request, HttpContext http,
            IdentityPhoneService phones, ILoggerFactory loggerFactory, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            try
            {
                return await phones.ConfirmRegistrationAsync(request.Mobile, request.Code,
                    request.Password, ct) ? Results.StatusCode(201) :
                    Results.BadRequest(new { error = "verification_failed" });
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                var frames = new StackTrace(exception, false).GetFrames()?
                    .Take(6)
                    .Select(frame => frame.GetMethod() is { } method
                        ? $"{method.DeclaringType?.Name}.{method.Name}"
                        : "unknown")
                    .ToArray() ?? [];
                loggerFactory.CreateLogger("ArginTaxPayer.Identity.OtpDelivery")
                    .LogError("OTP_REGISTRATION_PIPELINE_EXCEPTION exception_type={ExceptionType} frames={Frames}",
                        exception.GetType().FullName, string.Join(" > ", frames));
                return Results.Json(new { error = "registration_unavailable" },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        group.MapPost("/phone/login/complete", async (
            [FromBody] PhoneComplete request, HttpContext http,
            IdentityPhoneService phones, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var tokens = await phones.CompleteLoginAsync(request.Mobile, request.Code, ct);
            return tokens is null ? Results.Unauthorized() : Results.Ok(tokens);
        });

        group.MapPost("/phone/reset/complete", async (
            [FromBody] PhoneResetComplete request, HttpContext http,
            IdentityPhoneService phones, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            return await phones.ResetPasswordAsync(request.Mobile, request.Code,
                request.NewPassword, ct) ? Results.NoContent() :
                Results.BadRequest(new { error = "verification_failed" });
        });

        group.MapPost("/admin/customers", async (
            [FromBody] AdminCreateCustomerRequest request, HttpContext http,
            IdentityAdministratorUserProvisioning provisioning, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var result = await provisioning.CreateCustomerAsync(
                ReadBearer(http), request.Mobile, request.Email, request.Password, ct);
            return result switch
            {
                AdminUserCreationStatus.Created => Results.StatusCode(201),
                AdminUserCreationStatus.Unauthenticated => Results.Unauthorized(),
                AdminUserCreationStatus.Forbidden => Results.StatusCode(403),
                AdminUserCreationStatus.Conflict => Results.Conflict(new { error = "account_unavailable" }),
                _ => Results.BadRequest(new { error = "invalid_customer_data" })
            };
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

        group.MapPost("/roles/level2/nominate", async (
            [FromBody] AdministratorNominationRequest request, HttpContext http,
            IdentityAuthorizationService authorization, CancellationToken ct) =>
        {
            if (!IsLoopback(http)) return Results.NotFound();
            var result = await authorization.NominateLevel2Async(ReadBearer(http), request.Mobile, ct);
            return result switch
            {
                Level2RoleGrantResult.Unauthenticated => Results.Unauthorized(),
                Level2RoleGrantResult.Forbidden => Results.StatusCode(403),
                Level2RoleGrantResult.TargetNotFound => Results.NotFound(),
                Level2RoleGrantResult.Conflict => Results.Conflict(),
                _ => Results.Ok(new { status = "administrator_nominated" })
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
public sealed record MfaRevealRequest(string? Code);
public sealed record MfaCompleteRequest(string? Challenge, string? Code, bool UseRecoveryCode);

public sealed record Level2GrantRequest(Guid TargetUserId);
public sealed record AdministratorNominationRequest(string? Mobile);

public sealed record PhoneRequest(string? Mobile, string? Purpose);
public sealed record PhoneComplete(string? Mobile, string? Code);
public sealed record PhoneRegisterComplete(string? Mobile, string? Code, string? Password);
public sealed record PhoneResetComplete(string? Mobile, string? Code, string? NewPassword);

public sealed record AdminCreateCustomerRequest(string? Mobile, string? Email, string? Password);

public sealed record Level2MfaResetRequest(Guid TargetUserId);
