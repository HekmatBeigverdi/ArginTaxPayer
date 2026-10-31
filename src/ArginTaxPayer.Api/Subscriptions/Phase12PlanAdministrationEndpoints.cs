using System.Net;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Subscriptions;

public static class Phase12PlanAdministrationEndpoints
{
    public sealed record CreatePlanRequest(string Code, SubscriptionPlanKind Kind,
        int ValidityDays, long? InvoiceUnitLimit);

    public static IEndpointRouteBuilder MapPhase12PlanAdministrationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/subscriptions/plans", async (
            HttpContext http, [FromBody] CreatePlanRequest input,
            SubscriptionPlanAdministrationService service, CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress;
            if (ip is null || !(IPAddress.IsLoopback(ip) ||
                (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()))))
                return Results.NotFound();
            if (http.Request.Headers.ContainsKey("Origin")) return Results.StatusCode(403);
            if (http.Request.ContentLength is > 1024) return Results.StatusCode(413);
            var header = http.Request.Headers.Authorization.ToString();
            var bearer = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) &&
                         header.Length <= 4096 ? header[7..].Trim() : null;
            if (bearer is null) return Results.Unauthorized();
            try
            {
                var plan = await service.CreateAsync(bearer, input.Code, input.Kind,
                    input.ValidityDays, input.InvoiceUnitLimit, ct);
                return Results.Json(new {plan.Id, plan.Code, plan.Kind, plan.ValidityDays,
                    plan.InvoiceUnitLimit, plan.IsActive}, statusCode: 201);
            }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (ArgumentException) { return Results.BadRequest(); }
            catch (InvalidOperationException) { return Results.Conflict(); }
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
        return routes;
    }
}
