using System.Net;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using Microsoft.AspNetCore.Mvc;

namespace ArginTaxPayer.Api.Parties;

/// <summary>Loopback-only Development endpoints; production APIs require independent security review.</summary>
public static class PartyDevelopmentEndpoints
{
    public sealed record NaturalPersonWrite(string? NationalCode, string? FirstName,
        string? LastName, long ExpectedRevision = 0, bool Archive = false);
    public sealed record LegalEntityWrite(string? NationalIdentifier, string? RegisteredName,
        string? RegistrationNumber, long ExpectedRevision = 0, bool Archive = false);
    public sealed record DuplicateReviewWrite(Guid CandidatePartyId, Guid ExistingPartyId,
        PartyDuplicateDecision Decision, string? Reason);

    private static bool Local(HttpContext http) =>
        http.Connection.RemoteIpAddress is { } ip &&
        (IPAddress.IsLoopback(ip) || (ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4())));
    private static string? Token(HttpContext http)
    {
        var authorization = http.Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim() : null;
    }
    private static IResult Status(PartyDetailsDecision result) => result switch
    {
        PartyDetailsDecision.Unauthenticated => Results.Unauthorized(),
        PartyDetailsDecision.Forbidden => Results.StatusCode(403),
        PartyDetailsDecision.NotFound => Results.NotFound(),
        PartyDetailsDecision.Invalid => Results.BadRequest(new { error = "invalid_party_request" }),
        PartyDetailsDecision.Conflict => Results.Conflict(new { error = "party_conflict" }),
        _ => Results.Ok()
    };
    private static IResult Status(NaturalPersonResult result, bool created = false) => result.Decision switch
    {
        NaturalPersonDecision.Unauthenticated => Results.Unauthorized(),
        NaturalPersonDecision.Forbidden => Results.StatusCode(403),
        NaturalPersonDecision.NotFound => Results.NotFound(),
        NaturalPersonDecision.Invalid => Results.BadRequest(new { error = "invalid_natural_person" }),
        NaturalPersonDecision.Conflict => Results.Conflict(new { error = "party_conflict_or_stale_revision" }),
        _ when created => Results.Created($"/api/dev/customers/{result.Person!.CustomerUserId}/parties/{result.Person.Id}", result.Person),
        _ => Results.Ok(result.Person)
    };
    private static IResult Status(LegalEntityResult result, bool created = false) => result.Decision switch
    {
        LegalEntityDecision.Unauthenticated => Results.Unauthorized(),
        LegalEntityDecision.Forbidden => Results.StatusCode(403),
        LegalEntityDecision.NotFound => Results.NotFound(),
        LegalEntityDecision.Invalid => Results.BadRequest(new { error = "invalid_legal_entity" }),
        LegalEntityDecision.Conflict => Results.Conflict(new { error = "party_conflict_or_stale_revision" }),
        _ when created => Results.Created($"/api/dev/customers/{result.Person!.CustomerUserId}/parties/{result.Person.Id}", result.Person),
        _ => Results.Ok(result.Person)
    };
    private static IResult Status(PartyReviewStatus decision) => decision switch
    {
        PartyReviewStatus.Unauthenticated => Results.Unauthorized(),
        PartyReviewStatus.Forbidden => Results.StatusCode(403),
        PartyReviewStatus.NotFound => Results.NotFound(),
        PartyReviewStatus.Invalid => Results.BadRequest(new { error = "invalid_duplicate_review" }),
        PartyReviewStatus.Conflict => Results.Conflict(new { error = "duplicate_review_conflict" }),
        _ => Results.Ok()
    };

    public static IEndpointRouteBuilder MapPartyDevelopmentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/customers/{customerId:guid}/parties")
            .WithTags("Parties (Development)");
        group.MapGet("", async (Guid customerId, int? page, int? pageSize, string? query,
            PartyKind? kind, HttpContext http, PartyDirectoryService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.ListAsync(Token(http), customerId, page ?? 1, pageSize ?? 20, query, kind, ct);
            return result.Decision == PartyDetailsDecision.Success ? Results.Ok(result) : Status(result.Decision);
        });

        group.MapPost("/natural", async (Guid customerId, [FromBody] NaturalPersonWrite body,
            HttpContext http, NaturalPersonService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (body is null || body.ExpectedRevision != 0 || body.Archive)
                return Results.BadRequest(new { error = "invalid_create_request" });
            return Status(await svc.CreateAsync(Token(http), customerId,
                body.NationalCode, body.FirstName, body.LastName, ct), true);
        });

        group.MapPost("/legal", async (Guid customerId, [FromBody] LegalEntityWrite body,
            HttpContext http, LegalEntityService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (body is null || body.ExpectedRevision != 0 || body.Archive)
                return Results.BadRequest(new { error = "invalid_create_request" });
            return Status(await svc.CreateAsync(Token(http), customerId,
                body.NationalIdentifier, body.RegisteredName, body.RegistrationNumber, ct), true);
        });

        group.MapGet("/{partyId:guid}/natural", async (Guid customerId, Guid partyId,
            HttpContext http, NaturalPersonService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Status(await svc.GetAsync(Token(http), customerId, partyId, ct));
        });
        group.MapGet("/{partyId:guid}/legal", async (Guid customerId, Guid partyId,
            HttpContext http, LegalEntityService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            return Status(await svc.GetAsync(Token(http), customerId, partyId, ct));
        });
        group.MapPut("/{partyId:guid}/natural", async (Guid customerId, Guid partyId,
            [FromBody] NaturalPersonWrite body, HttpContext http, NaturalPersonService svc,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (body is null) return Results.BadRequest(new { error = "invalid_update_request" });
            return Status(await svc.UpdateAsync(Token(http), customerId, partyId,
                body.ExpectedRevision, body.NationalCode, body.FirstName, body.LastName, body.Archive, ct));
        });
        group.MapPut("/{partyId:guid}/legal", async (Guid customerId, Guid partyId,
            [FromBody] LegalEntityWrite body, HttpContext http, LegalEntityService svc,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (body is null) return Results.BadRequest(new { error = "invalid_update_request" });
            return Status(await svc.UpdateAsync(Token(http), customerId, partyId,
                body.ExpectedRevision, body.NationalIdentifier, body.RegisteredName,
                body.RegistrationNumber, body.Archive, ct));
        });

        group.MapPut("/{partyId:guid}/identity", async (
            Guid customerId, Guid partyId, [FromBody] PartyIdentityEditInput body,
            HttpContext http, PartyIdentityEditService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.UpdateAsync(Token(http), customerId, partyId, body, ct);
            return result.Status switch
            {
                PartyIdentityEditStatus.Unauthenticated => Results.Unauthorized(),
                PartyIdentityEditStatus.Forbidden => Results.StatusCode(403),
                PartyIdentityEditStatus.NotFound => Results.NotFound(),
                PartyIdentityEditStatus.Invalid => Results.BadRequest(new { error = "invalid_party_identity" }),
                PartyIdentityEditStatus.Conflict => Results.Conflict(new { error = "revision_or_shared_party_conflict" }),
                _ => Results.Ok(new { id = result.Party!.Id, kind = (int)result.Party.Kind,
                    normalizedIdentifier = result.Party.NormalizedIdentifier,
                    displayName = result.Party.DisplayName, revision = result.Party.Revision })
            };
        });

        group.MapGet("/{partyId:guid}/details", async (Guid customerId, Guid partyId,
            HttpContext http, PartyContactAddressService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.ListAsync(Token(http), customerId, partyId, ct);
            return result.Decision == PartyDetailsDecision.Success
                ? Results.Ok(new { result.Contacts, result.Addresses }) : Status(result.Decision);
        });
        group.MapPost("/{partyId:guid}/contacts", async (Guid customerId, Guid partyId,
            [FromBody] PartyContactInput body, HttpContext http, PartyContactAddressService svc,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.SaveContactAsync(Token(http), customerId, partyId, null, null, body, ct);
            return result.Decision == PartyDetailsDecision.Success
                ? Results.Created($"/api/dev/customers/{customerId}/parties/{partyId}/contacts/{result.Id}", result)
                : Status(result.Decision);
        });
        group.MapPut("/{partyId:guid}/contacts/{id:guid}", async (Guid customerId, Guid partyId, Guid id,
            [FromQuery] long expectedRevision, [FromBody] PartyContactInput body, HttpContext http,
            PartyContactAddressService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.SaveContactAsync(Token(http), customerId, partyId, id, expectedRevision, body, ct);
            return result.Decision == PartyDetailsDecision.Success ? Results.Ok(result) : Status(result.Decision);
        });
        group.MapDelete("/{partyId:guid}/contacts/{id:guid}", async (Guid customerId, Guid partyId, Guid id,
            [FromQuery] long expectedRevision, HttpContext http, PartyContactAddressService svc,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.DeactivateContactAsync(Token(http), customerId, partyId, id, expectedRevision, ct);
            return result.Decision == PartyDetailsDecision.Success ? Results.Ok(result) : Status(result.Decision);
        });
        group.MapPost("/{partyId:guid}/addresses", async (Guid customerId, Guid partyId,
            [FromBody] PartyAddressInput body, HttpContext http, PartyContactAddressService svc,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.SaveAddressAsync(Token(http), customerId, partyId, null, null, body, ct);
            return result.Decision == PartyDetailsDecision.Success
                ? Results.Created($"/api/dev/customers/{customerId}/parties/{partyId}/addresses/{result.Id}", result)
                : Status(result.Decision);
        });
        group.MapPut("/{partyId:guid}/addresses/{id:guid}", async (Guid customerId, Guid partyId, Guid id,
            [FromQuery] long expectedRevision, [FromBody] PartyAddressInput body, HttpContext http,
            PartyContactAddressService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.SaveAddressAsync(Token(http), customerId, partyId, id, expectedRevision, body, ct);
            return result.Decision == PartyDetailsDecision.Success ? Results.Ok(result) : Status(result.Decision);
        });
        group.MapDelete("/{partyId:guid}/addresses/{id:guid}", async (Guid customerId, Guid partyId, Guid id,
            [FromQuery] long expectedRevision, HttpContext http, PartyContactAddressService svc,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.DeactivateAddressAsync(Token(http), customerId, partyId, id, expectedRevision, ct);
            return result.Decision == PartyDetailsDecision.Success ? Results.Ok(result) : Status(result.Decision);
        });
        group.MapGet("/{partyId:guid}/duplicates", async (Guid customerId, Guid partyId,
            HttpContext http, PartyDeduplicationService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await svc.FindCandidatesAsync(Token(http), customerId, partyId, ct);
            return result.Status == PartyReviewStatus.Success
                ? Results.Ok(result.Matches) : Status(result.Status);
        });
        group.MapPost("/duplicates/reviews", async (Guid customerId, [FromBody] DuplicateReviewWrite body,
            HttpContext http, PartyDeduplicationService svc, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            if (body is null) return Results.BadRequest(new { error = "invalid_duplicate_review" });
            var result = await svc.ReviewAsync(Token(http), customerId, body.CandidatePartyId,
                body.ExistingPartyId, body.Decision, body.Reason, ct);
            return result.Status == PartyReviewStatus.Success ? Results.Ok(result) : Status(result.Status);
        });
        return routes;
    }
}
