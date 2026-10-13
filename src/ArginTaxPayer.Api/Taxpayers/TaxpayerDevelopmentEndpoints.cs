using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Taxpayers;

/// <summary>Development-only loopback endpoint. Production activation is a separate security gate.</summary>
public static class TaxpayerDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapTaxpayerDevelopmentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/taxpayers", async (
            [FromBody] CreateTaxpayerRequest request, HttpContext http,
            TaxpayerCreationService service, CancellationToken ct) =>
        {
            if (!(http.Connection.RemoteIpAddress is { } ip && (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))))
                return Results.NotFound();
            var header = http.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header[7..].Trim() : null;
            var result = await service.CreateAsync(token, request, ct);
            return result.Status switch
            {
                CreateTaxpayerStatus.Unauthenticated => Results.Unauthorized(),
                CreateTaxpayerStatus.Forbidden => Results.StatusCode(403),
                CreateTaxpayerStatus.Invalid => Results.BadRequest(new { error = "invalid_taxpayer" }),
                CreateTaxpayerStatus.Conflict => Results.Conflict(new { error = "taxpayer_exists" }),
                _ => Results.Created($"/api/dev/taxpayers/{result.Taxpayer!.Id}", result.Taxpayer)
            };
        }).WithTags("Taxpayers (Development)");
        routes.MapPut("/api/dev/taxpayers/{taxpayerId:guid}", async (
            Guid taxpayerId, [FromBody] UpdateTaxpayerProfileRequest request, HttpContext http,
            TaxpayerProfileLifecycleService service, CancellationToken ct) =>
        {
            if (!(http.Connection.RemoteIpAddress is { } ip &&
                (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))))
                return Results.NotFound();
            var header = http.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header[7..].Trim() : null;
            var result = await service.UpdateAsync(token, taxpayerId, request, ct);
            return result.Outcome switch
            {
                TaxpayerLifecycleOutcome.Unauthenticated => Results.Unauthorized(),
                TaxpayerLifecycleOutcome.Forbidden => Results.StatusCode(403),
                TaxpayerLifecycleOutcome.NotFound => Results.NotFound(),
                TaxpayerLifecycleOutcome.Invalid => Results.BadRequest(new { error = "invalid_taxpayer_transition" }),
                TaxpayerLifecycleOutcome.Conflict => Results.Conflict(new { error = "stale_taxpayer_revision" }),
                _ => Results.Ok(result.Profile)
            };
        }).WithTags("Taxpayers (Development)");
        return routes;
    }
}
