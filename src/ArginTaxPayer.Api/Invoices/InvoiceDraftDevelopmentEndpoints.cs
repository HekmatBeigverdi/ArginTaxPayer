using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Invoices;

/// <summary>
/// Development-only local invoice drafts. No taxpayer-system transmission
/// endpoint exists in Phase07. Always authenticate at the service boundary.
/// </summary>
public sealed record BulkInvoiceWorkspaceItem(string InvoiceKey, SaveInvoiceEntryRequest Draft);
public sealed record BulkInvoiceWorkspaceRequest(string BatchKey, BulkInvoiceWorkspaceItem[] Invoices);
public sealed record BulkInvoiceWorkspaceResult(string InvoiceKey, string Status, Guid? DraftId, string? Error);

public static class InvoiceDraftDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceDraftDevelopmentEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dev/invoices/header-options/{taxpayerId:guid}", async (
            Guid taxpayerId, HttpContext http, TaxpayerScopeService scope,
            ArginTaxPayerDbContext db, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var actor = await scope.ResolveAsync(Token(http), ct);
            if (actor.Decision == TaxpayerScopeDecision.Unauthenticated) return Results.Unauthorized();
            if (actor.Decision != TaxpayerScopeDecision.Allowed) return Results.StatusCode(403);
            var taxpayer = await scope.ScopedQuery(actor.ActorId,actor.Level1)
                .Where(p => p.Id == taxpayerId)
                .Select(p => new {p.Id,p.DisplayName,p.NormalizedIdentifier}).SingleOrDefaultAsync(ct);
            if (taxpayer is null) return Results.NotFound();
            var memories = await db.FiscalMemories.AsNoTracking()
                .Where(m => m.TaxpayerId == taxpayerId)
                .OrderBy(m => m.MemoryIdentifier)
                .Select(m => new {m.Id,m.MemoryIdentifier,m.Status})
                .ToArrayAsync(ct);
            return Results.Ok(new { taxpayer, memories });
        }).WithTags("Invoice Header Options (Development)");

        app.MapGet("/api/dev/invoices/form-metadata", async (
            HttpContext http, TaxpayerScopeService scope, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var actor = await scope.ResolveAsync(Token(http), ct);
            if (actor.Decision == TaxpayerScopeDecision.Unauthenticated)
                return Results.Unauthorized();
            if (actor.Decision != TaxpayerScopeDecision.Allowed)
                return Results.StatusCode(403);
            var matrix = OfficialV79InvoicePatternMatrix.Create();
            var entries = matrix.Entries.Select(e => new
            {
                type = (int)e.Type, pattern = e.Pattern,
                requiredFields = e.Type == ElectronicInvoiceType.First && e.Pattern == 4
                    ? new[] { "contractNumber" }
                    : e.Type == ElectronicInvoiceType.First && e.Pattern == 5
                    ? new[] { "serviceBillId" }
                    : e.Type == ElectronicInvoiceType.First && e.Pattern == 6
                    ? new[] { "flightType" }
                    : e.Pattern == 3 ? new[] { "goldFields" } : Array.Empty<string>()
            }).ToArray();
            return Results.Ok(new {
                edition = matrix.Source.Edition,
                verifiedForSubmission = matrix.Source.VerifiedForSubmission,
                entries
            });
        }).WithTags("Invoice Form Metadata (Development)");

        app.MapGet("/api/dev/invoice-workspaces/{id:guid}/history", async (
            Guid id, HttpContext http, InvoiceEntryWorkspaceService service,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var history = await service.HistoryAsync(Token(http),id,ct);
            return history.Decision == InvoiceApiDecision.Success
                ? Results.Ok(history.History) : Error(history.Decision);
        }).WithTags("Invoice Draft History (Development)");

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

        app.MapPost("/api/dev/invoice-workspaces/bulk", async (
            [FromBody] BulkInvoiceWorkspaceRequest input, HttpContext http,
            InvoiceEntryWorkspaceService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (input?.Invoices is not { Length: > 0 and <= 50 } ||
                input.BatchKey is not { Length: >= 8 and <= 64 } ||
                input.BatchKey.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_') ||
                input.Invoices.Any(x=>x.InvoiceKey is not { Length: >= 1 and <= 64 } ||
                    x.InvoiceKey.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) ||
                input.Invoices.Select(x=>x.InvoiceKey).Distinct(StringComparer.Ordinal).Count() !=
                    input.Invoices.Length)
                return Results.BadRequest(new { error = "invalid_bulk_manifest" });
            var results = new List<BulkInvoiceWorkspaceResult>();
            foreach (var invoice in input.Invoices)
            {
                if (invoice.Draft is null || invoice.Draft.Id is not null ||
                    invoice.Draft.ExpectedRevision != 0)
                {
                    results.Add(new(invoice.InvoiceKey,"invalid",null,"invalid_draft"));
                    continue;
                }
                try
                {
                    var draft = invoice.Draft with {
                        BulkImportKey = input.BatchKey + ":" + invoice.InvoiceKey,
                        BulkFingerprint = null, Id = null, ExpectedRevision = 0
                    };
                    var saved = await service.SaveAsync(Token(http),draft,ct);
                    var status = saved.Decision switch {
                        InvoiceApiDecision.Success => saved.Replayed ? "duplicate" : "created",
                        InvoiceApiDecision.Invalid => "invalid",
                        InvoiceApiDecision.Conflict => "conflict",
                        InvoiceApiDecision.Forbidden => "forbidden",
                        InvoiceApiDecision.NotFound => "not_found",
                        _ => "unauthorized"
                    };
                    results.Add(new(invoice.InvoiceKey,status,saved.Draft?.Id,
                        saved.Decision == InvoiceApiDecision.Success ? null :
                            saved.Decision.ToString()));
                }
                catch (DbUpdateException)
                {
                    results.Add(new(invoice.InvoiceKey,"failed",null,"database_error"));
                }
            }
            return Results.Ok(new { batchKey = input.BatchKey, results });
        }).WithTags("Controlled Invoice Draft Batch Import (Development)");

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
