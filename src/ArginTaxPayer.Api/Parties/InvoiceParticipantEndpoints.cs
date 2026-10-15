using System.Net;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Parties;

/// <summary>Development-only protected profile metadata, never official tax connection.</summary>
public static class InvoiceParticipantEndpoints
{
    public static IEndpointRouteBuilder MapInvoiceParticipantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev/customers/{customerId:guid}/invoice-details")
            .WithTags("Invoice Participants (Development)");
        group.MapGet("/{role}/{subjectId:guid}", async (Guid customerId, string role,
            Guid subjectId, HttpContext http, InvoiceParticipantDetailsService service, CancellationToken ct) =>
        {
            if (!Local(http) || role is not ("buyer" or "seller")) return Results.NotFound();
            return Result(await service.GetAsync(Token(http), customerId, subjectId, role == "seller", ct));
        });
        group.MapPut("/{role}/{subjectId:guid}", async (Guid customerId, string role,
            Guid subjectId, [FromBody] InvoiceParticipantWrite body, HttpContext http,
            InvoiceParticipantDetailsService service, CancellationToken ct) =>
        {
            if (!Local(http) || role is not ("buyer" or "seller")) return Results.NotFound();
            return Result(await service.SaveAsync(Token(http), customerId, subjectId, role == "seller", body, ct));
        });
        group.MapMethods("/{role}/{subjectId:guid}", new[] { "PATCH" }, async (
            Guid customerId, string role, Guid subjectId, [FromBody] JsonElement body,
            HttpContext http, InvoiceParticipantDetailsService service, CancellationToken ct) =>
        {
            if (!Local(http) || role is not ("buyer" or "seller")) return Results.NotFound();
            return Result(await service.PatchAsync(Token(http), customerId, subjectId,
                role == "seller", body, ct));
        });
        return app;
    }

    private static string? Token(HttpContext http)
    {
        var value = http.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : null;
    }
    private static bool Local(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) || ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    private static IResult Result(InvoiceParticipantResult result) => result.Outcome switch
    {
        InvoiceParticipantOutcome.Unauthenticated => Results.Unauthorized(),
        InvoiceParticipantOutcome.Forbidden => Results.StatusCode(403),
        InvoiceParticipantOutcome.Invalid => Results.BadRequest(new { error = "invalid_invoice_participant_details" }),
        InvoiceParticipantOutcome.NotFound => Results.NotFound(),
        InvoiceParticipantOutcome.Conflict => Results.Conflict(new { error = "stale_or_shared_party" }),
        _ => Results.Json(result.Details)
    };
}
