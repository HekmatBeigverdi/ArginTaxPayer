using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Mvc;

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
            IdentityCredentialService identity,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            if (!configuration.GetValue<bool>("Identity:AllowDevelopmentRegistration"))
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
            IdentityCredentialService identity,
            CancellationToken cancellationToken) =>
        {
            var valid = await identity.VerifyCredentialsAsync(request.Email, request.Password, cancellationToken);
            return valid
                ? Results.Ok(new { status = "credentials_verified", sessionIssued = false })
                : Results.Unauthorized();
        });

        return endpoints;
    }
}

public sealed record IdentityCredentials(string? Email, string? Password);
