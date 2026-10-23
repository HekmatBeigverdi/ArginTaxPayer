using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;

namespace ArginTaxPayer.Api.Invoices;

/// <summary>
/// Step16 loopback-only, authenticated and fail-closed dispatch UI boundary.
/// Read-only status and readiness; no production or sandbox send routes.
/// </summary>
public static class Phase10InvoiceDispatchDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapPhase10InvoiceDispatchDevelopmentEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/taxpayers/{taxpayerId:guid}/invoices/{invoiceId:guid}/dispatch")
            .WithTags("Invoice Dispatch (Development, Disabled)");

        group.MapGet("/status", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10InvoiceDispatchReadService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.GetAsync(Token(http), taxpayerId, invoiceId, ct);
            return result.Decision switch
            {
                InvoiceApiDecision.Success => Results.Ok(new {
                    environment = "Mock", submissionEnabled = false,
                    inquiryEnabled = false, fiscalAccepted = false,
                    status = result.Overview?.Status ?? "NotPrepared",
                    operationId = result.Overview?.OperationId,
                    invoiceRevision = result.Overview?.InvoiceRevision,
                    stateRevision = result.Overview?.StateRevision,
                    updatedAtUtc = result.Overview?.UpdatedAtUtc
                }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.Invalid => Results.BadRequest(new {error="invalid_invoice_id"}),
                _ => Results.NotFound()
            };
        });

        // The only control endpoint is an explicit fail-closed response.
        // It performs auth and scope verification before announcing unavailable.
        // This cannot be used to submit packets by guessing a taxpayer ID.
        group.MapPost("/send", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10InvoiceDispatchReadService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.GetAsync(Token(http), taxpayerId, invoiceId, ct);
            if (result.Decision == InvoiceApiDecision.Unauthenticated)
                return Results.Unauthorized();
            if (result.Decision == InvoiceApiDecision.Forbidden)
                return Results.StatusCode(403);
            if (result.Decision != InvoiceApiDecision.Success)
                return Results.NotFound();
            return Results.Json(new { error = "invoice_dispatch_not_enabled",
                environment = "Mock", submissionEnabled = false,
                fiscalAccepted = false }, statusCode: StatusCodes.Status503ServiceUnavailable);
        });
        return routes;
    }

    private static bool Local(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        return ip is not null && (IPAddress.IsLoopback(ip) ||
            ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    }

    private static string? Token(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header[7..].Trim() : null;
    }
}
