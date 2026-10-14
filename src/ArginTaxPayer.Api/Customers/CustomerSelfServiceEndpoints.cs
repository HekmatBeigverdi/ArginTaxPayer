using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Customers;

namespace ArginTaxPayer.Api.Customers;

/// <summary>Development-only and loopback-only proof of the customer self-service boundary.</summary>
public static class CustomerSelfServiceEndpoints
{
    public static IEndpointRouteBuilder MapCustomerSelfServiceEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dev/customer/self-service", async (
            HttpContext http, CustomerSelfServiceService service, CancellationToken ct) =>
        {
            if (http.Connection.RemoteIpAddress is not { } ip ||
                !(IPAddress.IsLoopback(ip) ||
                  ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
                return Results.NotFound();

            var header = http.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header[7..].Trim() : null;
            var result = await service.GetMineAsync(token, ct);
            return result.Decision switch
            {
                CustomerSelfServiceDecision.Unauthenticated => Results.Unauthorized(),
                CustomerSelfServiceDecision.Forbidden => Results.StatusCode(403),
                CustomerSelfServiceDecision.Invalid => Results.BadRequest(new { error = "invalid_self_service" }),
                _ => Results.Ok(result.Dashboard)
            };
        }).WithTags("Customer Self-Service (Development)");

        return routes;
    }
}
