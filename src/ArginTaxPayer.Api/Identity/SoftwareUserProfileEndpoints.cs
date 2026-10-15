using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Mvc;
namespace ArginTaxPayer.Api.Identity;

public static class SoftwareUserProfileEndpoints
{
    private static bool Local(HttpContext c) => c.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) || ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    private static string? Token(HttpContext c)
    {
        var value = c.Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : null;
    }
    private static IResult Respond(SoftwareProfileResult value) => value.Status switch
    {
        SoftwareProfileStatus.Unauthenticated => Results.Unauthorized(),
        SoftwareProfileStatus.Invalid => Results.BadRequest(new { error = "invalid_software_profile" }),
        SoftwareProfileStatus.Conflict => Results.Conflict(new { error = "profile_revision_conflict" }),
        _ => Results.Json(value.Data)
    };
    public static IEndpointRouteBuilder MapSoftwareUserProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/software-profile").WithTags("Software User Profile (Development)");
        group.MapGet("/me", async (HttpContext ctx, SoftwareUserProfileService service, CancellationToken ct) =>
            !Local(ctx) ? Results.NotFound() : Respond(await service.GetAsync(Token(ctx), ct)));
        group.MapPut("/me", async ([FromBody] SoftwareProfileWrite request,
            HttpContext ctx, SoftwareUserProfileService service, CancellationToken ct) =>
            !Local(ctx) ? Results.NotFound() : Respond(await service.SaveAsync(Token(ctx), request, ct)));
        group.MapPost("/logo", async (HttpContext ctx, SoftwareUserProfileService service, CancellationToken ct) =>
        {
            if (!Local(ctx)) return Results.NotFound();
            if (ctx.Request.ContentType != "image/png" || ctx.Request.ContentLength is null or > 350000)
                return Results.BadRequest(new { error = "invalid_logo_size_or_type" });
            await using var stream = new MemoryStream();
            await ctx.Request.Body.CopyToAsync(stream, ct);
            if (stream.Length > 350000) return Results.BadRequest(new { error = "logo_too_large" });
            var status = await service.SaveLogoAsync(Token(ctx), stream.ToArray(), ct);
            return status switch
            {
                SoftwareProfileStatus.Unauthenticated => Results.Unauthorized(),
                SoftwareProfileStatus.Invalid => Results.BadRequest(new { error = "invalid_logo_png_512" }),
                _ => Results.Ok(new { saved = true })
            };
        });
        group.MapGet("/logo", async (HttpContext ctx, SoftwareUserProfileService service, CancellationToken ct) =>
        {
            if (!Local(ctx)) return Results.NotFound();
            var (status, bytes) = await service.ReadLogoAsync(Token(ctx), ct);
            return status switch
            {
                SoftwareProfileStatus.Unauthenticated => Results.Unauthorized(),
                SoftwareProfileStatus.NotFound => Results.NotFound(),
                _ => Results.File(bytes!, "image/png", enableRangeProcessing: false)
            };
        });
        return routes;
    }
}
