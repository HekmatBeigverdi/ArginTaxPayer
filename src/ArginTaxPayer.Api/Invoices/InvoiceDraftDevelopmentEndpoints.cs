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
        app.MapGet("/api/dev/invoice-workspaces/{id:guid}/validation", async (
            Guid id, HttpContext http, InvoiceValidationReferenceService service,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.ValidateAsync(Token(http),id,ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.Validation) : Error(result.Decision);
        }).WithTags("Invoice Validation (Development)");

        app.MapGet("/api/dev/invoice-references/{taxpayerId:guid}", async (
            Guid taxpayerId, HttpContext http,
            InvoiceValidationReferenceService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.ListReferencesAsync(Token(http),taxpayerId,ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.References) : Error(result.Decision);
        }).WithTags("Invoice References (Development)");

        app.MapGet("/api/dev/invoice-workspaces", async (
            Guid? customerId, Guid? taxpayerId, int? page, int? pageSize,
            HttpContext http, InvoiceEntryWorkspaceService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.ListAsync(Token(http),customerId,taxpayerId,
                page ?? 1,pageSize ?? 50,ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.Drafts) : Error(result.Decision);
        }).WithTags("Resumable Invoice Drafts (Development)");

        app.MapGet("/api/dev/invoice-workspaces/{id:guid}", async (
            Guid id,HttpContext http,InvoiceEntryWorkspaceService service,CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.GetAsync(Token(http),id,ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.Draft) : Error(result.Decision);
        }).WithTags("Resumable Invoice Drafts (Development)");

        app.MapPost("/api/dev/invoice-workspaces", async (
            [FromBody] SaveInvoiceEntryRequest request,
            HttpContext http,InvoiceEntryWorkspaceService service,CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.SaveAsync(Token(http),request,ct);
            return result.Decision == InvoiceApiDecision.Success
                ? Results.Ok(result.Draft) : Error(result.Decision);
        }).WithTags("Resumable Invoice Drafts (Development)");

        app.MapGet("/api/dev/invoices", async (Guid? customerId, Guid? taxpayerId,
            int? page, int? pageSize, string? reviewState, string? search,
            HttpContext http, InvoiceDraftApplicationService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.ListVisibleAsync(Token(http), customerId, taxpayerId,
                page ?? 1, pageSize ?? 50, ct, reviewState, search);
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
