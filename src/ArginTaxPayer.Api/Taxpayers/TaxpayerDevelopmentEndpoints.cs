using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Taxpayers;

/// <summary>Development-only loopback endpoint. Production activation is a separate security gate.</summary>
public static class TaxpayerDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapTaxpayerDevelopmentEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/dev/taxpayers", async (
            [FromBody] CreateTaxpayerRequest request, HttpContext http,
            TaxpayerCreationService service, CancellationToken ct) =>
        {
            if (!(http.Connection.RemoteIpAddress is { } ip && (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))))
                return Results.NotFound();
            var header = http.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header[7..].Trim() : null;
            var result = await service.CreateAsync(token, request, ct);
            return result.Status switch
            {
                CreateTaxpayerStatus.Unauthenticated => Results.Unauthorized(),
                CreateTaxpayerStatus.Forbidden => Results.StatusCode(403),
                CreateTaxpayerStatus.Invalid => Results.BadRequest(new { error = "invalid_taxpayer" }),
                CreateTaxpayerStatus.Conflict => Results.Conflict(new { error = "taxpayer_exists" }),
                _ => Results.Created($"/api/dev/taxpayers/{result.Taxpayer!.Id}", result.Taxpayer)
            };
        }).WithTags("Taxpayers (Development)");
        routes.MapPut("/api/dev/taxpayers/{taxpayerId:guid}", async (
            Guid taxpayerId, [FromBody] UpdateTaxpayerProfileRequest request, HttpContext http,
            TaxpayerProfileLifecycleService service, CancellationToken ct) =>
        {
            if (!(http.Connection.RemoteIpAddress is { } ip &&
                (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())))))
                return Results.NotFound();
            var header = http.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header[7..].Trim() : null;
            var result = await service.UpdateAsync(token, taxpayerId, request, ct);
            return result.Outcome switch
            {
                TaxpayerLifecycleOutcome.Unauthenticated => Results.Unauthorized(),
                TaxpayerLifecycleOutcome.Forbidden => Results.StatusCode(403),
                TaxpayerLifecycleOutcome.NotFound => Results.NotFound(),
                TaxpayerLifecycleOutcome.Invalid => Results.BadRequest(new { error = "invalid_taxpayer_transition" }),
                TaxpayerLifecycleOutcome.Conflict => Results.Conflict(new { error = "stale_taxpayer_revision" }),
                _ => Results.Ok(result.Profile)
            };
        }).WithTags("Taxpayers (Development)");
        routes.MapPost("/api/dev/taxpayers/{taxpayerId:guid}/memories", async (
            Guid taxpayerId, [FromBody] RegisterFiscalMemoryRequest request, HttpContext http,
            FiscalMemoryManagementService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.RegisterAsync(Bearer(http), taxpayerId, request, ct);
            return result.Status switch
            {
                FiscalMemoryOperationStatus.Unauthenticated => Results.Unauthorized(),
                FiscalMemoryOperationStatus.Forbidden => Results.StatusCode(403),
                FiscalMemoryOperationStatus.Invalid => Results.BadRequest(new { error = "invalid_memory_identifier" }),
                FiscalMemoryOperationStatus.NotFound => Results.NotFound(),
                FiscalMemoryOperationStatus.Conflict => Results.Conflict(new { error = "memory_identifier_conflict" }),
                FiscalMemoryOperationStatus.RequiresApprovedBindingReview => Results.Conflict(new { error = "binding_review_required" }),
                _ => Results.Created($"/api/dev/taxpayers/{taxpayerId}/memories/{result.Memory!.Id}", result.Memory)
            };
        }).WithTags("Fiscal Memories (Development)");

        routes.MapGet("/api/dev/taxpayers/{taxpayerId:guid}/memories", async (
            Guid taxpayerId, HttpContext http, FiscalMemoryManagementService service,
            CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.ListAsync(Bearer(http), taxpayerId, ct);
            return result.Status switch
            {
                FiscalMemoryOperationStatus.Unauthenticated => Results.Unauthorized(),
                FiscalMemoryOperationStatus.Forbidden => Results.StatusCode(403),
                FiscalMemoryOperationStatus.NotFound => Results.NotFound(),
                _ => Results.Ok(result.Memories)
            };
        }).WithTags("Fiscal Memories (Development)");

        routes.MapPut("/api/dev/taxpayers/{taxpayerId:guid}/memories/{memoryId:guid}/status", async (
            Guid taxpayerId, Guid memoryId, [FromBody] ChangeFiscalMemoryStatusRequest request,
            HttpContext http, FiscalMemoryManagementService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.TransitionAsync(Bearer(http), taxpayerId, memoryId, request, ct);
            return result.Status switch
            {
                FiscalMemoryOperationStatus.Unauthenticated => Results.Unauthorized(),
                FiscalMemoryOperationStatus.Forbidden => Results.StatusCode(403),
                FiscalMemoryOperationStatus.NotFound => Results.NotFound(),
                FiscalMemoryOperationStatus.Invalid => Results.BadRequest(new { error = "invalid_memory_transition" }),
                FiscalMemoryOperationStatus.Conflict => Results.Conflict(new { error = "stale_memory_revision" }),
                FiscalMemoryOperationStatus.RequiresApprovedBindingReview => Results.Conflict(new { error = "binding_review_required" }),
                FiscalMemoryOperationStatus.AuthorityVerificationUnavailable =>
                    Results.Conflict(new { error = "authority_verification_not_available" }),
                _ => Results.Ok(result.Memory)
            };
        }).WithTags("Fiscal Memories (Development)");

        routes.MapGet("/api/dev/taxpayers/{taxpayerId:guid}/memories/{memoryId:guid}/certificates", async (
            Guid taxpayerId, Guid memoryId, HttpContext http,
            FiscalCertificateMetadataService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.ListAsync(Bearer(http), taxpayerId, memoryId, ct);
            return result.Status switch
            {
                CertificateMetadataReadStatus.Unauthenticated => Results.Unauthorized(),
                CertificateMetadataReadStatus.Forbidden => Results.StatusCode(403),
                CertificateMetadataReadStatus.NotFound => Results.NotFound(),
                _ => Results.Ok(result.Certificates)
            };
        }).WithTags("Certificates (Development)");

        // Deprecated taxpayer-to-Level2 delegation endpoints intentionally removed.
        // Ownership must be CustomerUser -> PartnerLevel2, never Taxpayer -> Level2.
        // Historical grant rows remain in DB for review, but cannot authorize new access.

        routes.MapGet("/api/dev/taxpayers", async (
            HttpContext http, TaxpayerScopeService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.ListAsync(Bearer(http), ct);
            return result.Decision switch
            {
                TaxpayerScopeDecision.Unauthenticated => Results.Unauthorized(),
                TaxpayerScopeDecision.Forbidden => Results.StatusCode(403),
                _ => Results.Ok(result.Profiles)
            };
        }).WithTags("Taxpayer Scope (Development)");

        routes.MapGet("/api/dev/taxpayers/{taxpayerId:guid}", async (
            Guid taxpayerId, HttpContext http, TaxpayerScopeService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.GetAsync(Bearer(http), taxpayerId, ct);
            return result.Decision switch
            {
                TaxpayerScopeDecision.Unauthenticated => Results.Unauthorized(),
                TaxpayerScopeDecision.Forbidden => Results.StatusCode(403),
                TaxpayerScopeDecision.NotFound => Results.NotFound(),
                _ => Results.Ok(result.Profile)
            };
        }).WithTags("Taxpayer Scope (Development)");

        routes.MapPatch("/api/dev/taxpayers/{taxpayerId:guid}/display-name", async (
            Guid taxpayerId, [FromBody] ScopedRenameRequest request,
            HttpContext http, TaxpayerScopeService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.RenameAsync(Bearer(http), taxpayerId, request, ct);
            return result.Decision switch
            {
                TaxpayerScopeDecision.Unauthenticated => Results.Unauthorized(),
                TaxpayerScopeDecision.Forbidden => Results.StatusCode(403),
                TaxpayerScopeDecision.NotFound => Results.NotFound(),
                TaxpayerScopeDecision.Conflict => Results.Conflict(new { error = "stale_revision_or_revoked_assignment" }),
                TaxpayerScopeDecision.Invalid => Results.BadRequest(new { error = "invalid_display_name" }),
                _ => Results.Ok(result.Profile)
            };
        }).WithTags("Taxpayer Scope (Development)");

        routes.MapGet("/api/dev/customer-owners", async (
            HttpContext http, CustomerCommercialOwnershipService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.ListAsync(Bearer(http), ct);
            return result.Decision switch
            {
                CustomerOwnershipDecision.Unauthenticated => Results.Unauthorized(),
                CustomerOwnershipDecision.Forbidden => Results.StatusCode(403),
                _ => Results.Ok(result.Owners)
            };
        }).WithTags("Customer Commercial Ownership (Development)");

        routes.MapGet("/api/dev/customer-owners/{customerId:guid}", async (
            Guid customerId, HttpContext http, CustomerCommercialOwnershipService service,
            CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.GetAsync(Bearer(http), customerId, ct);
            return result.Decision switch
            {
                CustomerOwnershipDecision.Unauthenticated => Results.Unauthorized(),
                CustomerOwnershipDecision.Forbidden => Results.StatusCode(403),
                CustomerOwnershipDecision.NotFound => Results.NotFound(),
                _ => Results.Ok(result.Owner)
            };
        }).WithTags("Customer Commercial Ownership (Development)");

        routes.MapPost("/api/dev/customer-owners/{customerId:guid}/transfer", async (
            Guid customerId, [FromBody] TransferCustomerOwnerRequest request,
            HttpContext http, CustomerCommercialOwnershipService service, CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.TransferAsync(Bearer(http), customerId, request, ct);
            return result.Decision switch
            {
                CustomerOwnershipDecision.Unauthenticated => Results.Unauthorized(),
                CustomerOwnershipDecision.Forbidden => Results.StatusCode(403),
                CustomerOwnershipDecision.Invalid => Results.BadRequest(new { error = "invalid_customer_transfer" }),
                CustomerOwnershipDecision.NotFound => Results.NotFound(),
                CustomerOwnershipDecision.Conflict => Results.Conflict(new { error = "stale_customer_owner_revision" }),
                _ => Results.Ok(result.Owner)
            };
        }).WithTags("Customer Commercial Ownership (Development)");

        routes.MapGet("/api/dev/customer-owners/{customerId:guid}/history", async (
            Guid customerId, HttpContext http, CustomerCommercialOwnershipService service,
            CancellationToken ct) =>
        {
            if (!IsLocal(http)) return Results.NotFound();
            var result = await service.HistoryAsync(Bearer(http), customerId, ct);
            return result.Decision switch
            {
                CustomerOwnershipDecision.Unauthenticated => Results.Unauthorized(),
                CustomerOwnershipDecision.Forbidden => Results.StatusCode(403),
                CustomerOwnershipDecision.NotFound => Results.NotFound(),
                _ => Results.Ok(result.History)
            };
        }).WithTags("Customer Commercial Ownership (Development)");

        return routes;
    }

    private static bool IsLocal(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())));

    private static string? Bearer(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..].Trim() : null;
    }
}
