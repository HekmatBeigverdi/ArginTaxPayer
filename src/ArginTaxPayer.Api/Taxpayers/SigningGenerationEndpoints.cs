using System.Security.Cryptography;
using System.Text.Json;
using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Domain.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;

namespace ArginTaxPayer.Api.Taxpayers;

public static class SigningGenerationEndpoints
{
    public const int MaximumBodyBytes = 8192;

    public static IEndpointRouteBuilder MapSigningGenerationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGroup("/api/dev/signing/{taxpayerId:guid}/{memoryId:guid}")
            .ExcludeFromDescription().RequireRateLimiting("signing-import")
            .MapPost("/generate", GenerateAsync);
        return routes;
    }

    internal static async Task<IResult> GenerateAsync(Guid taxpayerId, Guid memoryId, HttpContext http,
        ISigningKeyInitialHandoffService service, IConfiguration config, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
        if (!http.Request.HasJsonContentType()) return Results.StatusCode(415);
        if (http.Request.ContentLength > MaximumBodyBytes) return Results.StatusCode(413);
        if (config["SigningKeyGeneration:Enabled"] != "true" ||
            config["SigningKeyProtection:Enabled"] != "true")
            return Failure(SigningGenerationStatus.Unavailable);

        byte[]? body = null;
        try
        {
            body = new byte[MaximumBodyBytes + 1];
            var count = 0;
            while (count < body.Length)
            {
                var read = await http.Request.Body.ReadAsync(body.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            if (count > MaximumBodyBytes) return Results.StatusCode(413);
            using var document = JsonDocument.Parse(body.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 3 });
            var json = document.RootElement;
            if (json.ValueKind != JsonValueKind.Object) return Failure(SigningGenerationStatus.Invalid);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in json.EnumerateObject())
                if (!names.Add(field.Name) || field.Name is not ("operationId" or "consentVersion" or "acceptServerCustody"))
                    return Failure(SigningGenerationStatus.Invalid);
            if (!json.TryGetProperty("operationId", out var operationValue) ||
                operationValue.ValueKind != JsonValueKind.String || !operationValue.TryGetGuid(out var operation) ||
                operation == Guid.Empty || !json.TryGetProperty("consentVersion", out var consentValue) ||
                consentValue.ValueKind != JsonValueKind.String ||
                consentValue.GetString() != SigningOperationPolicy.ConsentVersion ||
                !json.TryGetProperty("acceptServerCustody", out var custodyValue) ||
                custodyValue.ValueKind != JsonValueKind.True)
                return Failure(SigningGenerationStatus.Invalid);

            using var generated = await service.GenerateWithInitialHandoffAsync(Bearer(http),
                new(taxpayerId, memoryId, operation, SigningOperationPolicy.LegacyProfile,
                    SigningOperationPolicy.ConsentVersion, true), ct);
            if (generated.Handoff is null)
            {
                if (generated.Result.Status == SigningGenerationStatus.Replayed)
                    return Results.Json(new { status = "replayed", privateHandoffAvailable = false,
                        publicKey = generated.Result.PublicKey }, statusCode: 200);
                return Failure(generated.Result.Status);
            }

            var package = SigningKeyDownloadPackage.Create(generated.Handoff);
            var publicKey = generated.Result.PublicKey;
            if (publicKey is null)
            {
                CryptographicOperations.ZeroMemory(package);
                return Failure(SigningGenerationStatus.IntegrityFailure);
            }
            http.Response.Headers["X-Signing-Key-Fingerprint"] = publicKey.FingerprintSha256;
            http.Response.Headers["X-Signing-Key-Version"] = publicKey.KeyVersionId.ToString("D");
            return Results.File(package, "application/zip", $"argin-signing-{publicKey.KeyVersionId:N}.zip");
        }
        catch (JsonException) { return Failure(SigningGenerationStatus.Invalid); }
        catch (ArgumentException) { return Failure(SigningGenerationStatus.Invalid); }
        catch (OperationCanceledException) { return Failure(SigningGenerationStatus.RecoveryRequired); }
        catch (Exception) { return Results.Json(new { error = "service_unavailable" }, statusCode: 503); }
        finally
        {
            if (body is not null) CryptographicOperations.ZeroMemory(body);
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
        SigningGenerationStatus.Invalid => "invalid_request",
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
