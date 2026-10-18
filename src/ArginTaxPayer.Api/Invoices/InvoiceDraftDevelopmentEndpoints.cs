using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Invoices;

/// <summary>
/// Development-only local invoice drafts. No taxpayer-system transmission
/// endpoint exists in Phase07. Always authenticate at the service boundary.
/// </summary>
public static class InvoiceDraftDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceDraftDevelopmentEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dev/invoices", async (Guid? customerId, Guid? taxpayerId,
            HttpContext http, InvoiceDraftApplicationService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.ListVisibleAsync(Token(http), customerId, taxpayerId, ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.Data) : Error(result.Decision);
        }).WithTags("Invoice Drafts (Development)");

        var group = app.MapGroup("/api/dev/taxpayers/{taxpayerId:guid}/invoices")
            .WithTags("Invoice Drafts (Development)");

        group.MapGet("", async (Guid taxpayerId, HttpContext http,
            InvoiceDraftApplicationService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.ListAsync(Token(http), taxpayerId, ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.Data) : Error(result.Decision);
        });

        group.MapGet("/{invoiceId:guid}", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, InvoiceDraftApplicationService service,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.GetAsync(Token(http), taxpayerId, invoiceId, ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.Invoice) : Error(result.Decision);
        });

        group.MapPost("", async (Guid taxpayerId,
            [FromBody] CreateInvoiceDraftRequest request, HttpContext http,
            InvoiceDraftApplicationService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.CreateAsync(Token(http), taxpayerId, request, ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Created(
                    $"/api/dev/taxpayers/{taxpayerId}/invoices/{result.Invoice!.Id}",
                    result.Invoice)
                : Error(result.Decision);
        });

        return app;
    }

    private static bool Local(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) ||
         ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));

    private static string? Token(HttpContext http)
    {
        var value = http.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? value[7..].Trim() : null;
    }

    private static IResult Error(InvoiceApiDecision status) => status switch
    {
        InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
        InvoiceApiDecision.Forbidden => Results.StatusCode(403),
        InvoiceApiDecision.NotFound => Results.NotFound(),
        InvoiceApiDecision.Invalid => Results.BadRequest(new { error = "invalid_invoice_draft" }),
        InvoiceApiDecision.Conflict => Results.Conflict(new { error = "invoice_conflict" }),
        _ => Results.StatusCode(500)
    };
}
