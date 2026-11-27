using System.Net;
using ArginTaxPayer.Api.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Invoices;

public static class InvoiceReportEndpoints
{
    public static void MapInvoiceReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dev/reports/invoices", async (HttpContext http,
            [FromQuery] Guid? customerId, [FromQuery] string? period,
            [FromQuery] string? from, [FromQuery] string? to,
            [FromQuery] int? subject, [FromQuery] string? environment,
            Phase15DashboardReadService service, CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress;
            if (ip is null || !IPAddress.IsLoopback(ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip))
                return Results.NotFound();
            http.Response.Headers.CacheControl = "private, no-store";
            var header = http.Request.Headers.Authorization.ToString();
            if (header.Length > 4096 || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();

            try
            {
                var window = Phase15DashboardReadService.ResolveWindow(period, from, to, DateTime.UtcNow);
                return Results.Ok(await service.InvoiceReportAsync(header[7..].Trim(), customerId,
                    window, subject, environment, ct));
            }
            catch (UnauthorizedAccessException) { return Results.StatusCode(403); }
            catch (KeyNotFoundException) { return Results.NotFound(); }
            catch (ArgumentException) { return Results.BadRequest(new { error = "invalid_filter" }); }
        }).ExcludeFromDescription().RequireRateLimiting("signing-import");
    }
}
