using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Parties;

namespace ArginTaxPayer.Api.Parties;

/// <summary>Explicit read-only, DEVELOPMENT + loopback-only fiscal directory.</summary>
public static class FiscalParticipantDirectoryEndpoints
{
    public static IEndpointRouteBuilder MapFiscalParticipantDirectoryEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dev/fiscal-participants", async (
            HttpContext http, int? page, int? pageSize, string? type, int? kind,
            string? query, FiscalParticipantDirectoryService service, CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress;
            if (ip is null || !(IPAddress.IsLoopback(ip) ||
                ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))
                return Results.NotFound();
            var auth = http.Request.Headers.Authorization.ToString();
            var token = auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? auth[7..].Trim() : null;
            var result = await service.ListAsync(token, page ?? 1, pageSize ?? 20,
                type, kind, query, ct);
            return result.Decision switch
            {
                PartyDetailsDecision.Success => Results.Ok(result),
                PartyDetailsDecision.Unauthenticated => Results.Unauthorized(),
                PartyDetailsDecision.Forbidden => Results.StatusCode(403),
                _ => Results.BadRequest(new { error = "invalid_fiscal_directory_request" })
            };
        }).WithTags("Fiscal Participants (Development)");
        return app;
    }
}
