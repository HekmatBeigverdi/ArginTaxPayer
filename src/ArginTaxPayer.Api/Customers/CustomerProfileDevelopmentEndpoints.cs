using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Customers;

public static class CustomerProfileDevelopmentEndpoints
{
    private static bool Local(HttpContext c) => c.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())));
    private static string? Bearer(HttpContext c)
    {
        var v = c.Request.Headers.Authorization.ToString();
        return v.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? v[7..].Trim() : null;
    }
    private static IResult Map(CustomerProfileResult r) => r.Decision switch
    {
        CustomerProfileDecision.Unauthenticated => Results.Unauthorized(),
        CustomerProfileDecision.Forbidden => Results.StatusCode(403),
        CustomerProfileDecision.Invalid => Results.BadRequest(new { error = "invalid_customer_profile" }),
        CustomerProfileDecision.NotFound => Results.NotFound(),
        CustomerProfileDecision.Conflict => Results.Conflict(new { error = "customer_profile_conflict" }),
        _ => Results.Ok(r.Profile)
    };
    public static IEndpointRouteBuilder MapCustomerProfileDevelopmentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dev/customers/{customerId:guid}/profile",
            async (Guid customerId, HttpContext http, CustomerAccountProfileService service, CancellationToken ct) =>
                !Local(http) ? Results.NotFound() : Map(await service.GetAsync(Bearer(http), customerId, ct)))
            .WithTags("Customer Profiles (Development)");
        routes.MapPut("/api/dev/customers/{customerId:guid}/profile",
            async (Guid customerId, [FromBody] CustomerProfileUpdate input, HttpContext http,
                CustomerAccountProfileService service, CancellationToken ct) =>
                !Local(http) ? Results.NotFound() : Map(await service.SaveAsync(Bearer(http), customerId, input, ct)))
            .WithTags("Customer Profiles (Development)");
        return routes;
    }
}
