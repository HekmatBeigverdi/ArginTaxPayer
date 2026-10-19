using System.Net;
using ArginTaxPayer.Domain.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Invoices;

/// <summary>
/// Development-only local review API. Internal approval is NEVER fiscal acceptance.
/// Authorization, current ownership and CAS are enforced in the command service.
/// </summary>
public static class InvoiceReviewDevelopmentEndpoints
{
    public sealed record ReviewActionRequest(long ExpectedReviewRevision,
        string? IdempotencyKey, string? Reason = null);

    public static IEndpointRouteBuilder MapInvoiceReviewDevelopmentEndpoints(
        this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev/invoices/{invoiceId:guid}/review")
            .WithTags("Invoice Review (Development)");

        group.MapPost("/submit", async (Guid invoiceId,
            [FromBody] ReviewActionRequest request, HttpContext http,
            InvoiceReviewCommandService service, CancellationToken ct) =>
            Run(http, invoiceId, request, InvoiceReviewAction.SubmitForReview, service, ct));

        group.MapPost("/request-changes", async (Guid invoiceId,
            [FromBody] ReviewActionRequest request, HttpContext http,
            InvoiceReviewCommandService service, CancellationToken ct) =>
            Run(http, invoiceId, request, InvoiceReviewAction.RequestChanges, service, ct));

        group.MapPost("/approve", async (Guid invoiceId,
            [FromBody] ReviewActionRequest request, HttpContext http,
            InvoiceReviewCommandService service, CancellationToken ct) =>
            Run(http, invoiceId, request, InvoiceReviewAction.Approve, service, ct));

        return app;
    }

    private static async Task<IResult> Run(HttpContext http, Guid invoiceId,
        ReviewActionRequest? input, InvoiceReviewAction action,
        InvoiceReviewCommandService service, CancellationToken ct)
    {
        if (!IsLocal(http)) return Results.NotFound();
        if (input is null) return Results.BadRequest(new { error = "invalid_invoice_review_request" });
        var result = await service.ExecuteAsync(Token(http),
            new InvoiceReviewCommand(invoiceId, input.ExpectedReviewRevision,
                action, input.Reason, input.IdempotencyKey), ct);
        return MapResult(result);
    }

    public static IResult MapResult(InvoiceReviewCommandResult result) =>
        result.Decision switch
        {
            InvoiceReviewCommandDecision.Success => Results.Ok(new {
                state = result.State?.ToString(), revision = result.Revision,
                authoritySubmitted = false
            }),
            InvoiceReviewCommandDecision.Unauthenticated => Results.Unauthorized(),
            InvoiceReviewCommandDecision.Forbidden => Results.StatusCode(403),
            InvoiceReviewCommandDecision.NotFound => Results.NotFound(),
            InvoiceReviewCommandDecision.Invalid => Results.BadRequest(new {
                error = "invalid_invoice_review_request"
            }),
            InvoiceReviewCommandDecision.Conflict => Results.Conflict(new {
                error = "invoice_review_revision_conflict"
            }),
            InvoiceReviewCommandDecision.IdempotencyKeyConflict => Results.Conflict(new {
                error = "invoice_review_idempotency_conflict"
            }),
            _ => Results.StatusCode(500)
        };

    private static bool IsLocal(HttpContext http)
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
