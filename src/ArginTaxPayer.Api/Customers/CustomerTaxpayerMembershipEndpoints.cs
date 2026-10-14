using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Customers;

public static class CustomerTaxpayerMembershipEndpoints
{
    private static bool Local(HttpContext ctx) => ctx.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())));
    private static string? Token(HttpContext ctx)
    {
        var auth = ctx.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim() : null;
    }
    private static IResult Denied(MembershipDecision d) => d switch
    {
        MembershipDecision.Unauthenticated => Results.Unauthorized(),
        MembershipDecision.Forbidden => Results.StatusCode(403),
        MembershipDecision.Invalid => Results.BadRequest(new { error = "invalid_membership" }),
        MembershipDecision.NotFound => Results.NotFound(),
        MembershipDecision.Conflict => Results.Conflict(new { error = "membership_conflict" }),
        _ => Results.Ok()
    };

    public static IEndpointRouteBuilder MapCustomerTaxpayerMembershipEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/dev/customers/{customerId:guid}/taxpayer-memberships",
            async (Guid customerId, bool? includeHistory, HttpContext ctx,
                CustomerTaxpayerMembershipService svc, CancellationToken ct) =>
            {
                if (!Local(ctx)) return Results.NotFound();
                var result = await svc.ListAsync(Token(ctx), customerId, includeHistory ?? false, ct);
                return result.Decision == MembershipDecision.Success
                    ? Results.Ok(result.Memberships) : Denied(result.Decision);
            }).WithTags("Customer Taxpayer Memberships (Development)");

        routes.MapPost("/api/dev/customers/{customerId:guid}/taxpayer-memberships/{taxpayerId:guid}",
            async (Guid customerId, Guid taxpayerId, HttpContext ctx,
                CustomerTaxpayerMembershipService svc, CancellationToken ct) =>
            {
                if (!Local(ctx)) return Results.NotFound();
                var result = await svc.LinkAsync(Token(ctx), customerId, taxpayerId, ct);
                return result.Decision == MembershipDecision.Success
                    ? Results.Created($"/api/dev/customers/{customerId}/taxpayer-memberships/{result.Membership!.Id}",
                        result.Membership) : Denied(result.Decision);
            }).WithTags("Customer Taxpayer Memberships (Development)");

        routes.MapDelete("/api/dev/customers/{customerId:guid}/taxpayer-memberships/{membershipId:guid}",
            async (Guid customerId, Guid membershipId, [FromQuery] long expectedRevision,
                HttpContext ctx, CustomerTaxpayerMembershipService svc, CancellationToken ct) =>
            {
                if (!Local(ctx)) return Results.NotFound();
                var result = await svc.UnlinkAsync(Token(ctx), customerId, membershipId, expectedRevision, ct);
                return result.Decision == MembershipDecision.Success
                    ? Results.Ok(result.Membership) : Denied(result.Decision);
            }).WithTags("Customer Taxpayer Memberships (Development)");
        return routes;
    }
}
