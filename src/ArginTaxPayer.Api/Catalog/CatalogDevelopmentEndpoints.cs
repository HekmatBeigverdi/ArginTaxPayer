using System.Net;
using ArginTaxPayer.Domain.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Catalog;

/// <summary>Development + loopback only; no production-facing catalog endpoints.</summary>
public static class CatalogDevelopmentEndpoints
{
    public sealed record ProductWrite(string InternalCode, string Title,
        CatalogItemKind Kind, Guid BaseUnitId, long ExpectedRevision = 0, bool Discontinue = false);
    public sealed record ServiceWrite(string InternalCode, string Title,
        Guid BaseUnitId, long ExpectedRevision = 0, bool Discontinue = false);
    public sealed record MappingWrite(Guid OfficialEntryId, DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc = null);

    private static bool Local(HttpContext http) => http.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) || ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    private static string? Token(HttpContext http)
    {
        var auth = http.Request.Headers.Authorization.ToString();
        return auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..].Trim() : null;
    }
    private static IResult Status(CustomerProductResult result, bool create = false) =>
        result.Decision switch
        {
            CustomerProductDecision.Unauthenticated => Results.Unauthorized(),
            CustomerProductDecision.Forbidden => Results.StatusCode(403),
            CustomerProductDecision.NotFound => Results.NotFound(),
            CustomerProductDecision.Invalid or CustomerProductDecision.UnitUnavailable =>
                Results.BadRequest(new { error = result.Decision.ToString() }),
            CustomerProductDecision.Conflict => Results.Conflict(new { error = "product_conflict" }),
            _ when create => Results.Created($"/api/dev/catalog/customers/{result.Product!.CustomerUserId}/products/{result.Product.Id}", result.Product),
            _ => Results.Ok(result.Product)
        };
    private static IResult Status(CustomerServiceResult result, bool create = false) =>
        result.Decision switch
        {
            CustomerServiceDecision.Unauthenticated => Results.Unauthorized(),
            CustomerServiceDecision.Forbidden => Results.StatusCode(403),
            CustomerServiceDecision.NotFound => Results.NotFound(),
            CustomerServiceDecision.Invalid or CustomerServiceDecision.UnitUnavailable =>
                Results.BadRequest(new { error = result.Decision.ToString() }),
            CustomerServiceDecision.Conflict => Results.Conflict(new { error = "service_conflict" }),
            _ when create => Results.Created($"/api/dev/catalog/customers/{result.Service!.CustomerUserId}/services/{result.Service.Id}", result.Service),
            _ => Results.Ok(result.Service)
        };
    private static IResult Status(TaxMappingResult result, bool create = false) =>
        result.Decision switch
        {
            TaxMappingDecision.Unauthenticated => Results.Unauthorized(),
            TaxMappingDecision.Forbidden => Results.StatusCode(403),
            TaxMappingDecision.NotFound => Results.NotFound(),
            TaxMappingDecision.Invalid or TaxMappingDecision.WrongKind or
                TaxMappingDecision.UnverifiedOfficialCode => Results.BadRequest(new { error = result.Decision.ToString() }),
            TaxMappingDecision.Conflict or TaxMappingDecision.Overlap =>
                Results.Conflict(new { error = result.Decision.ToString() }),
            _ when create => Results.Created($"/api/dev/catalog/mappings/{result.Mapping!.Id}", result.Mapping),
            _ => Results.Ok(result.Mapping)
        };
    private static IResult Page<T>(CatalogPage<T> result) =>
        result.Decision switch
        {
            CatalogQueryDecision.Unauthenticated => Results.Unauthorized(),
            CatalogQueryDecision.Forbidden => Results.StatusCode(403),
            CatalogQueryDecision.Invalid => Results.BadRequest(new { error = "invalid_catalog_query" }),
            _ => Results.Ok(result)
        };

    public static IEndpointRouteBuilder MapCatalogDevelopmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/catalog").WithTags("Catalog (Development)");
        group.MapGet("/units", async (HttpContext http, int? page, int? pageSize,
            string? query, CatalogSearchService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Page(await svc.UnitsAsync(Token(http), page ?? 1, pageSize ?? 30, query, ct));
        });

        group.MapGet("/official", async (HttpContext http, int? page, int? pageSize,
            string? query, int? kind, CatalogSearchService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Page(await svc.OfficialAsync(Token(http), page ?? 1, pageSize ?? 20, query, kind, ct));
        });

        group.MapGet("/customers/{customerId:guid}/products", async (Guid customerId,
            HttpContext http, int? page, int? pageSize, string? query,
            CatalogSearchService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Page(await svc.ProductsAsync(Token(http), customerId, page ?? 1, pageSize ?? 20, query, ct));
        });
        group.MapPost("/customers/{customerId:guid}/products", async (Guid customerId,
            [FromBody] ProductWrite input, HttpContext http, CustomerProductService svc,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (input is null || input.ExpectedRevision != 0 || input.Discontinue)
                return Results.BadRequest(new { error = "invalid_create" });
            return Status(await svc.CreateAsync(Token(http), customerId,
                new CatalogProductInput(input.InternalCode, input.Title, input.Kind, input.BaseUnitId), ct), true);
        });
        group.MapGet("/customers/{customerId:guid}/products/{id:guid}", async (
            Guid customerId, Guid id, HttpContext http, CustomerProductService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Status(await svc.GetAsync(Token(http), customerId, id, ct));
        });
        group.MapPut("/customers/{customerId:guid}/products/{id:guid}", async (
            Guid customerId, Guid id, [FromBody] ProductWrite input,
            HttpContext http, CustomerProductService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (input is null) return Results.BadRequest(new { error = "invalid_update" });
            return Status(await svc.UpdateAsync(Token(http), customerId, id, input.ExpectedRevision,
                new CatalogProductInput(input.InternalCode, input.Title, input.Kind, input.BaseUnitId),
                input.Discontinue, ct));
        });

        group.MapGet("/customers/{customerId:guid}/services", async (Guid customerId,
            HttpContext http, int? page, int? pageSize, string? query,
            CatalogSearchService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Page(await svc.ServicesAsync(Token(http), customerId, page ?? 1, pageSize ?? 20, query, ct));
        });
        group.MapPost("/customers/{customerId:guid}/services", async (Guid customerId,
            [FromBody] ServiceWrite input, HttpContext http,
            CustomerServiceCatalogService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (input is null || input.ExpectedRevision != 0 || input.Discontinue)
                return Results.BadRequest(new { error = "invalid_create" });
            return Status(await svc.CreateAsync(Token(http), customerId,
                new CatalogServiceInput(input.InternalCode, input.Title, input.BaseUnitId), ct), true);
        });
        group.MapGet("/customers/{customerId:guid}/services/{id:guid}", async (
            Guid customerId, Guid id, HttpContext http, CustomerServiceCatalogService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Status(await svc.GetAsync(Token(http), customerId, id, ct));
        });
        group.MapPut("/customers/{customerId:guid}/services/{id:guid}", async (
            Guid customerId, Guid id, [FromBody] ServiceWrite input,
            HttpContext http, CustomerServiceCatalogService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (input is null) return Results.BadRequest(new { error = "invalid_update" });
            return Status(await svc.UpdateAsync(Token(http), customerId, id, input.ExpectedRevision,
                new CatalogServiceInput(input.InternalCode, input.Title, input.BaseUnitId),
                input.Discontinue, ct));
        });

        group.MapGet("/customers/{customerId:guid}/items/{itemId:guid}/tax-mappings", async (
            Guid customerId, Guid itemId, HttpContext http,
            TaxIdentifierMappingService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var (decision, history) = await svc.HistoryAsync(Token(http), customerId, itemId, ct);
            return decision switch
            {
                TaxMappingDecision.Success => Results.Ok(history),
                TaxMappingDecision.Unauthenticated => Results.Unauthorized(),
                TaxMappingDecision.Forbidden => Results.StatusCode(403),
                TaxMappingDecision.NotFound => Results.NotFound(),
                _ => Results.BadRequest(new { error = decision.ToString() })
            };
        });
        group.MapPost("/customers/{customerId:guid}/items/{itemId:guid}/tax-mappings", async (
            Guid customerId, Guid itemId, [FromBody] MappingWrite input,
            HttpContext http, TaxIdentifierMappingService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (input is null) return Results.BadRequest(new { error = "invalid_mapping" });
            return Status(await svc.AssignAsync(Token(http), customerId, itemId,
                input.OfficialEntryId, input.EffectiveFromUtc, input.EffectiveToUtc, ct), true);
        });
        group.MapPut("/customers/{customerId:guid}/tax-mappings/{mappingId:guid}/close", async (
            Guid customerId, Guid mappingId, [FromQuery] long expectedRevision,
            [FromQuery] DateTime closedAtUtc, HttpContext http,
            TaxIdentifierMappingService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Status(await svc.CloseAsync(Token(http), customerId, mappingId,
                expectedRevision, closedAtUtc, ct));
        });
        return routes;
    }
}
