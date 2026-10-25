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
    public sealed record InternalValidationRequest(long ExpectedRevision);
    public sealed record ReviewActionRequest(long ExpectedReviewRevision,
        string? IdempotencyKey, string? Reason = null);

    public static IEndpointRouteBuilder MapInvoiceReviewDevelopmentEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/taxpayers/{taxpayerId:guid}/invoices/{invoiceId:guid}/validate-internal",
            async (Guid taxpayerId, Guid invoiceId, InternalValidationRequest request,
                HttpContext http, Phase10InternalInvoiceValidationService service,
                CancellationToken ct) =>
            {
                if (!IsLocal(http)) return Results.NotFound();
                http.Response.Headers.CacheControl = "no-store";
                var result = await service.ValidateAsync(Token(http), taxpayerId,
                    invoiceId, request.ExpectedRevision, ct);
                return result.Decision switch
                {
                    InvoiceApiDecision.Success => Results.Ok(new {
                        status = result.Code, authoritySubmitted = false }),
                    InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                    InvoiceApiDecision.Forbidden => Results.Json(new {
                        error = result.Code == "membership_inactive"
                            ? "membership_inactive" : "forbidden"
                    }, statusCode: 403),
                    InvoiceApiDecision.NotFound => Results.NotFound(),
                    InvoiceApiDecision.Conflict => Results.Conflict(new { error = result.Code }),
                    _ => Results.BadRequest(new { error = result.Code })
                };
            });
        app.MapPost("/api/dev/taxpayers/{taxpayerId:guid}/invoices/{invoiceId:guid}/finalize-internal",
            async (Guid taxpayerId, Guid invoiceId, InternalValidationRequest request,
                HttpContext http, Phase10InvoiceFinalizationService service,
                CancellationToken ct) =>
            {
                if (!IsLocal(http)) return Results.NotFound();
                http.Response.Headers.CacheControl = "no-store";
                var result = await service.FinalizeAsync(Token(http), taxpayerId, invoiceId,
                    request.ExpectedRevision, ct);
                return result.Decision switch
                {
                    InvoiceApiDecision.Success => Results.Ok(new {
                        status = result.Code, authoritySubmitted = false }),
                    InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                    InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                    InvoiceApiDecision.NotFound => Results.NotFound(),
                    InvoiceApiDecision.Conflict => Results.Conflict(new { error = result.Code }),
                    _ => Results.BadRequest(new { error = result.Code })
                };
            });
        var group = app.MapGroup("/api/dev/invoices/{invoiceId:guid}/review")
            .WithTags("Invoice Review (Development)");

        group.MapPost("/submit", (Guid invoiceId,
            [FromBody] ReviewActionRequest request, HttpContext http,
            InvoiceReviewCommandService service, CancellationToken ct) =>
            Run(http, invoiceId, request, InvoiceReviewAction.SubmitForReview, service, ct));

        group.MapPost("/request-changes", (Guid invoiceId,
            [FromBody] ReviewActionRequest request, HttpContext http,
            InvoiceReviewCommandService service, CancellationToken ct) =>
            Run(http, invoiceId, request, InvoiceReviewAction.RequestChanges, service, ct));

        group.MapPost("/approve", (Guid invoiceId,
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
