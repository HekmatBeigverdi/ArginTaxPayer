using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Api.Identity;

namespace ArginTaxPayer.Api.Taxpayers;

public static class SigningImportEndpoints
{
    public const int MaximumBodyBytes = 98304;
    private static bool Configured(IConfiguration config) =>
        config["SigningKeyImport:Enabled"] == "true" && config["SigningKeyProtection:Enabled"] == "true";

    public static IEndpointRouteBuilder MapSigningImportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/signing/{taxpayerId:guid}/{memoryId:guid}")
            .ExcludeFromDescription().RequireRateLimiting("signing-import");
        group.MapGet("", async (Guid taxpayerId, Guid memoryId, HttpContext http,
            EfSigningOperationRepository journal, IConfiguration config, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
                return Results.NotFound();
            try
            {
                var state = await journal.ReadImportOnboardingAsync(Bearer(http), taxpayerId, memoryId, ct);
                if (state.Status != SigningGenerationStatus.Replayed) return Failure(state.Status);
                return Results.Ok(new { configured = Configured(config), state.FreshLogin,
                    state.OperationId, state.HasKey, state.FingerprintSha256, state.LifecycleState,
                    portalVerified = false });
            }
            catch (Exception) { return Results.Json(new { error = "service_unavailable" }, statusCode: 503); }
        });
        group.MapPost("", ImportAsync);
        return routes;
    }

    public static async Task<IResult> ImportAsync(Guid taxpayerId, Guid memoryId, HttpContext http,
        EfSigningOperationRepository journal, ISigningKeyImportService service,
        IConfiguration config, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        // This bearer-only loopback API accepts no browser cookies. The BFF checks Origin.
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        if (!http.Request.HasJsonContentType()) return Results.StatusCode(415);
        if (http.Request.ContentLength > MaximumBodyBytes) return Results.StatusCode(413);
        byte[]? body = null;
        byte[]? encoded = null;
        char[]? password = null;
        try
        {
            var bearer = Bearer(http);
            var state = await journal.ReadImportOnboardingAsync(bearer, taxpayerId, memoryId, ct);
            if (state.Status != SigningGenerationStatus.Replayed) return Failure(state.Status);
            if (!state.FreshLogin) return Results.Json(new { error = "reauthentication_required" }, statusCode: 403);
            if (!Configured(config)) return Results.Json(new { error = "custody_unavailable" }, statusCode: 503);
            body = new byte[MaximumBodyBytes + 1];
            var count = 0;
            while (count < body.Length)
            {
                var read = await http.Request.Body.ReadAsync(body.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            if (count > MaximumBodyBytes) return Results.StatusCode(413);
            using var document = JsonDocument.Parse(body.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
            var json = document.RootElement;
            if (json.ValueKind != JsonValueKind.Object) return Failure(SigningGenerationStatus.Invalid);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in json.EnumerateObject())
                if (!names.Add(field.Name) || field.Name is not
                    ("operationId" or "privateKey" or "password" or "publicKey" or "acceptServerCustody"))
                    return Failure(SigningGenerationStatus.Invalid);
            if (!json.TryGetProperty("operationId", out var operationValue) ||
                operationValue.ValueKind != JsonValueKind.String || !operationValue.TryGetGuid(out var operation) ||
                operation == Guid.Empty || !json.TryGetProperty("acceptServerCustody", out var consent) ||
                consent.ValueKind != JsonValueKind.True || !json.TryGetProperty("privateKey", out var privateValue) ||
                privateValue.ValueKind != JsonValueKind.String)
                return Failure(SigningGenerationStatus.Invalid);
            var pem = privateValue.GetString()!;
            if (pem.Length is < 1 or > 65536 || pem.Any(c => c > 127))
                return Failure(SigningGenerationStatus.Invalid);
            var secret = "";
            if (json.TryGetProperty("password", out var passwordValue))
            {
                if (passwordValue.ValueKind != JsonValueKind.String) return Failure(SigningGenerationStatus.Invalid);
                secret = passwordValue.GetString()!;
                if (secret.Length > 1024) return Failure(SigningGenerationStatus.Invalid);
            }
            string? publicPem = null;
            if (json.TryGetProperty("publicKey", out var publicValue))
            {
                if (publicValue.ValueKind != JsonValueKind.String) return Failure(SigningGenerationStatus.Invalid);
                publicPem = publicValue.GetString();
                if (publicPem is { Length: > 16384 }) return Failure(SigningGenerationStatus.Invalid);
                if (string.IsNullOrWhiteSpace(publicPem)) publicPem = null;
            }
            encoded = Encoding.ASCII.GetBytes(pem);
            password = secret.ToCharArray();
            var format = pem.TrimStart().StartsWith("-----BEGIN ENCRYPTED PRIVATE KEY-----", StringComparison.Ordinal)
                ? SigningPrivateKeyFormat.EncryptedPkcs8Pem : SigningPrivateKeyFormat.Pkcs8Pem;
            using var input = new SensitiveKeyInput(format, encoded, password);
            var result = await service.ImportAsync(bearer, new(taxpayerId, memoryId, operation,
                SigningOperationPolicy.LegacyProfile, SigningOperationPolicy.ConsentVersion, true, publicPem), input, ct);
            return result.Status is SigningGenerationStatus.Created or SigningGenerationStatus.Replayed
                ? Results.Json(new { publicKey = result.PublicKey, publicKeyCompared = publicPem is not null,
                    portalVerified = false }, statusCode: result.Status == SigningGenerationStatus.Created ? 201 : 200)
                : Failure(result.Status);
        }
        catch (JsonException) { return Failure(SigningGenerationStatus.Invalid); }
        catch (ArgumentException) { return Failure(SigningGenerationStatus.Invalid); }
        // No exception messages, request bodies or secret-bearing DTOs enter logs/responses.
        catch (Exception) { return Results.Json(new { error = "service_unavailable" }, statusCode: 503); }
        finally
        {
            if (body is not null) CryptographicOperations.ZeroMemory(body);
            if (encoded is not null) CryptographicOperations.ZeroMemory(encoded);
            if (password is not null) Array.Clear(password);
        }
    }

    private static string? Bearer(HttpContext http)
    {
        var value = http.Request.Headers.Authorization.ToString();
        return value.Length <= 4096 && value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? value[7..].Trim() : null;
    }

    private static IResult Failure(SigningGenerationStatus status) => Results.Json(new { error = status switch
    {
        SigningGenerationStatus.Unauthenticated => "unauthorized",
        SigningGenerationStatus.Forbidden => "forbidden",
        SigningGenerationStatus.NotFound => "not_found",
        SigningGenerationStatus.Invalid => "invalid_key_or_public_mismatch",
        SigningGenerationStatus.Conflict => "key_change_requires_review",
        SigningGenerationStatus.RecoveryRequired => "recovery_required",
        SigningGenerationStatus.IntegrityFailure => "integrity_failure",
        _ => "custody_unavailable"
    } }, statusCode: status switch
    {
        SigningGenerationStatus.Unauthenticated => 401, SigningGenerationStatus.Forbidden => 403,
        SigningGenerationStatus.NotFound => 404, SigningGenerationStatus.Invalid => 400,
        SigningGenerationStatus.Conflict => 409, SigningGenerationStatus.RecoveryRequired => 409,
        _ => 503
    });
}
