using System.Net;
using ArginTaxPayer.Application.Invoices;
using Microsoft.Extensions.Configuration;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;

namespace ArginTaxPayer.Api.Invoices;

public sealed record SandboxFiscalSerialReservationRequest(long ExpectedRevision);

/// <summary>
/// Step16 loopback-only, authenticated and fail-closed dispatch UI boundary.
/// Read-only status and readiness; no production or sandbox send routes.
/// </summary>
public static class Phase10InvoiceDispatchDevelopmentEndpoints
{
    public static IEndpointRouteBuilder MapPhase10InvoiceDispatchDevelopmentEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/taxpayers/{taxpayerId:guid}/invoices/{invoiceId:guid}/dispatch")
            .WithTags("Invoice Dispatch (Development, Disabled)");

        group.MapPost("/cancel-and-inquire",async(Guid taxpayerId,Guid invoiceId,
            HttpContext http,Phase10SandboxCancellationService service,CancellationToken ct)=>{
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var result=await service.SendAsync(Token(http),taxpayerId,invoiceId,ct);
            return result.Decision switch{
                InvoiceApiDecision.Success=>Results.Ok(new{
                    invoiceId=result.InvoiceId,status=result.Status,
                    referenceNumber=result.ReferenceNumber,inquiryCode=result.InquiryCode,
                    code=result.Code}),
                InvoiceApiDecision.Unauthenticated=>Results.Unauthorized(),
                InvoiceApiDecision.Forbidden=>Results.StatusCode(403),
                InvoiceApiDecision.NotFound=>Results.NotFound(),
                InvoiceApiDecision.Conflict=>Results.Conflict(new{
                    error=result.Code,invoiceId=result.InvoiceId,status=result.Status}),
                _=>Results.BadRequest(new{
                    error=result.Code,invoiceId=result.InvoiceId,status=result.Status})
            };
        });
        group.MapGet("/cancellation-eligibility",async(Guid taxpayerId,Guid invoiceId,
            HttpContext http,Phase10CancellationEligibilityService service,CancellationToken ct)=>{
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var value=await service.ReadAsync(Token(http),taxpayerId,invoiceId,ct);
            return value.Decision switch{
                InvoiceApiDecision.Success=>Results.Ok(new{
                    eligible=value.Eligible,fiscalTaxId=value.FiscalTaxId,code=value.Code}),
                InvoiceApiDecision.Unauthenticated=>Results.Unauthorized(),
                InvoiceApiDecision.Forbidden=>Results.StatusCode(403),
                InvoiceApiDecision.NotFound=>Results.NotFound(),
                _=>Results.Conflict(new{eligible=false,code=value.Code})
            };
        });
        group.MapGet("/lineage",async(Guid taxpayerId,Guid invoiceId,HttpContext http,
            Phase10InvoiceLineageReadService service,CancellationToken ct)=>{
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var data=await service.ReadAsync(Token(http),taxpayerId,invoiceId,ct);
            return data.Decision switch {
                InvoiceApiDecision.Success=>Results.Ok(data.Value),
                InvoiceApiDecision.Unauthenticated=>Results.Unauthorized(),
                InvoiceApiDecision.Forbidden=>Results.StatusCode(403),
                _=>Results.NotFound()
            };
        });
        group.MapGet("/print-preview", async (Guid taxpayerId,Guid invoiceId,
            HttpContext http,Phase10InvoicePrintPreviewService preview,CancellationToken ct)=>{
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var result=await preview.ReadAsync(Token(http),taxpayerId,invoiceId,ct);
            return result.Decision switch{
                InvoiceApiDecision.Success=>Results.Ok(result.Preview),
                InvoiceApiDecision.Unauthenticated=>Results.Unauthorized(),
                InvoiceApiDecision.Forbidden=>Results.StatusCode(403),
                InvoiceApiDecision.NotFound=>Results.NotFound(),
                _=>Results.BadRequest(new{error="invoice_preview_unavailable"})
            };
        });

        group.MapGet("/status", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, IConfiguration configuration,
            Phase10InvoiceDispatchReadService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.GetAsync(Token(http), taxpayerId, invoiceId, ct);
            return result.Decision switch
            {
                InvoiceApiDecision.Success => Results.Ok(new {
                    environment = "Sandbox",
                    submissionEnabled = configuration["SandboxInvoiceLegacy:Enabled"] == "true" &&
                        configuration["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] == "true" &&
                        result.Overview?.Status == "Reserved",
                    inquiryEnabled = result.Overview?.Status == "Acknowledged" &&
                        result.Overview?.ReferenceNumber is not null,
                    fiscalAccepted = false,
                    referenceNumber = result.Overview?.ReferenceNumber,
                    status = result.Overview?.Status ?? "NotPrepared",
                    operationId = result.Overview?.OperationId,
                    invoiceRevision = result.Overview?.InvoiceRevision,
                    stateRevision = result.Overview?.StateRevision,
                    updatedAtUtc = result.Overview?.UpdatedAtUtc
                }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.Invalid => Results.BadRequest(new {error="invalid_invoice_id"}),
                _ => Results.NotFound()
            };
        });

        group.MapGet("/calculation-audit", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10InvoiceCalculationAuditService service,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.AuditAsync(Token(http), taxpayerId, invoiceId, ct);
            return result.Decision switch
            {
                InvoiceApiDecision.Success => Results.Ok(new {
                    invoiceRevision = result.Report!.InvoiceRevision,
                    canResend = result.Report.CanResend,
                    findings = result.Report.Findings.Select(x => new {
                        code = x.Code, field = x.Field, message = x.Message,
                        expected = x.Expected, actual = x.Actual
                    })
                }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.Invalid => Results.BadRequest(new { error = "invalid_invoice_id" }),
                _ => Results.NotFound()
            };
        });

        group.MapPost("/inquiry-by-uid", async (Guid taxpayerId,Guid invoiceId,
            HttpContext http,Phase10LegacyUidInquiryService inquiry,CancellationToken ct) =>
        {
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var outcome=await inquiry.InquireAsync(Token(http),taxpayerId,invoiceId,ct);
            return outcome.Decision switch {
                InvoiceApiDecision.Success=>Results.Ok(new {
                    safeCode=outcome.SafeCode,referenceNumber=outcome.ReferenceNumber,
                    authoritySubmitted=false,fiscalAccepted=false }),
                InvoiceApiDecision.Unauthenticated=>Results.Unauthorized(),
                InvoiceApiDecision.Forbidden=>Results.StatusCode(403),
                InvoiceApiDecision.NotFound=>Results.NotFound(),
                _=>Results.BadRequest(new {error=outcome.SafeCode})
            };
        });

        group.MapGet("/inquiry-by-reference", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10LegacyReferenceInquiryService inquiry, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var outcome = await inquiry.ReadAsync(Token(http), taxpayerId, invoiceId, ct);
            return outcome.Decision switch {
                InvoiceApiDecision.Success => Results.Ok(new {
                    safeCode=outcome.SafeCode, referenceNumber=outcome.ReferenceNumber,
                    authorityResult=outcome.AuthorityResult,
                    fiscalAccepted=false, authoritySubmitted=false }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.NotFound => Results.NotFound(),
                _ => Results.BadRequest(new { error=outcome.SafeCode })
            };
        });

        group.MapPost("/correction-draft", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, LegacyInvoiceCorrectionDraftService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var outcome = await service.CreateAsync(Token(http), taxpayerId, invoiceId, ct);
            return outcome.Decision switch {
                InvoiceApiDecision.Success => Results.Ok(new {
                    workspaceId=outcome.WorkspaceId, authoritySubmitted=false,
                    issueDateAdjusted=outcome.IssueDateAdjusted }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.NotFound => Results.NotFound(),
                InvoiceApiDecision.Conflict => Results.Conflict(new { error=outcome.SafeCode }),
                _ => Results.BadRequest(new { error=outcome.SafeCode })
            };
        });

        group.MapPost("/inquiry-by-reference", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10LegacyReferenceInquiryService inquiry, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var outcome = await inquiry.InquireAsync(Token(http), taxpayerId, invoiceId, ct);
            return outcome.Decision switch {
                InvoiceApiDecision.Success => Results.Ok(new {
                    safeCode=outcome.SafeCode, referenceNumber=outcome.ReferenceNumber,
                    authorityResult=outcome.AuthorityResult,
                    fiscalAccepted=false, authoritySubmitted=false }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.NotFound => Results.NotFound(),
                _ => Results.BadRequest(new { error=outcome.SafeCode })
            };
        });

        group.MapGet("/history", async (Guid taxpayerId,Guid invoiceId,
            HttpContext http,Phase10InvoiceDispatchHistoryService history,CancellationToken ct) =>
        {
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var result=await history.ReadAsync(Token(http),taxpayerId,invoiceId,ct);
            return result.Decision switch {
                InvoiceApiDecision.Success => Results.Ok(result.Value),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.Invalid => Results.BadRequest(new {error="invalid_invoice_id"}),
                _ => Results.NotFound()
            };
        });

        group.MapGet("/readiness", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10LiveSendReadinessService readiness,
            CancellationToken ct) =>
        {
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var result=await readiness.ReadAsync(Token(http),taxpayerId,invoiceId,ct);
            return result.Decision switch {
                InvoiceApiDecision.Success=>Results.Ok(result.Readiness),
                InvoiceApiDecision.Unauthenticated=>Results.Unauthorized(),
                InvoiceApiDecision.Forbidden=>Results.StatusCode(403),
                InvoiceApiDecision.Invalid=>Results.BadRequest(new{error="invalid_invoice_id"}),
                _=>Results.NotFound()
            };
        });

        group.MapPost("/admission", async (Guid taxpayerId, Guid invoiceId,
            SandboxSubmissionAdmissionRequest request, HttpContext http,
            Phase10SandboxSubmissionAdmissionService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.EvaluateAsync(Token(http), taxpayerId, invoiceId, request, ct);
            return result.Decision switch
            {
                InvoiceApiDecision.Success => Results.Ok(result.Value),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.Invalid => Results.BadRequest(new { error = "invalid_admission_request" }),
                _ => Results.NotFound()
            };
        });

        group.MapPost("/serial-reservation", async (Guid taxpayerId, Guid invoiceId,
            SandboxFiscalSerialReservationRequest request, HttpContext http,
            SandboxInvoiceFiscalSerialRepository serials, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var result = await serials.ReserveAsync(Token(http), taxpayerId, invoiceId,
                request.ExpectedRevision, ct);
            return result.Status switch
            {
                SandboxSerialStatus.Reserved or SandboxSerialStatus.IdenticalReplay =>
                    Results.Ok(new {
                        status = result.Status.ToString(),
                        reservationId = result.ReservationId,
                        fiscalTaxId = result.FiscalTaxId,
                        serial = result.Serial
                    }),
                SandboxSerialStatus.NotFinalized => Results.Conflict(new {
                    error = "invoice_not_finalized"
                }),
                SandboxSerialStatus.Conflict => Results.Conflict(new {
                    error = "invoice_revision_conflict"
                }),
                SandboxSerialStatus.NotFound => Results.NotFound(),
                SandboxSerialStatus.Denied => Results.StatusCode(403),
                _ => Results.BadRequest(new { error = "invalid_serial_reservation" })
            };
        });

        group.MapPost("/prepare-legacy", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10LegacyInvoicePreparationService preparation,
            CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var result = await preparation.PrepareAsync(Token(http),taxpayerId,invoiceId,ct);
            return result.Decision switch
            {
                InvoiceApiDecision.Success => Results.Ok(new {
                    status = result.SafeCode, operationId = result.OperationId,
                    environment = "Sandbox", authoritySubmitted = false,
                    wirePreviewJson = result.WirePreviewJson
                }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.NotFound => Results.NotFound(),
                InvoiceApiDecision.Conflict => Results.Conflict(new { error = result.SafeCode }),
                _ => Results.BadRequest(new { error = result.SafeCode })
            };
        });

        // The only control endpoint is an explicit fail-closed response.
        // It performs auth and scope verification before announcing unavailable.
        // This cannot be used to submit packets by guessing a taxpayer ID.
        group.MapPost("/arm", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, Phase10DispatchArmService service, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            http.Response.Headers.CacheControl = "no-store";
            var result = await service.ArmAsync(Token(http),taxpayerId,invoiceId,ct);
            return result.Decision switch
            {
                InvoiceApiDecision.Success => Results.Ok(new { status = "Reserved",
                    safeCode = result.Code, authoritySubmitted = false }),
                InvoiceApiDecision.Unauthenticated => Results.Unauthorized(),
                InvoiceApiDecision.Forbidden => Results.StatusCode(403),
                InvoiceApiDecision.NotFound => Results.NotFound(),
                InvoiceApiDecision.Conflict => Results.Conflict(new { error = result.Code }),
                _ => Results.BadRequest(new { error = result.Code })
            };
        });

        group.MapPost("/send-and-inquire", async (Guid taxpayerId,Guid invoiceId,
            HttpContext http,Phase10OneClickSandboxSubmissionService workflow,CancellationToken ct)=>{
            if(!Local(http))return Results.NotFound();
            http.Response.Headers.CacheControl="no-store";
            var result=await workflow.SendAsync(Token(http),taxpayerId,invoiceId,ct);
            return result.Decision switch{
                InvoiceApiDecision.Success=>Results.Ok(new{status=result.Status,
                    safeCode=result.Code,referenceNumber=result.ReferenceNumber,
                    inquiryCode=result.InquiryCode}),
                InvoiceApiDecision.Unauthenticated=>Results.Unauthorized(),
                InvoiceApiDecision.Forbidden=>Results.StatusCode(403),
                InvoiceApiDecision.NotFound=>Results.NotFound(),
                InvoiceApiDecision.Conflict=>Results.Conflict(new{error=result.Code,
                    status=result.Status}),
                _=>Results.BadRequest(new{error=result.Code})
            };
        });

        group.MapPost("/send", async (Guid taxpayerId, Guid invoiceId,
            HttpContext http, IConfiguration configuration,
            Phase10InvoiceDispatchReadService service,
            IPhase10LegacyOutboxWorker worker, CancellationToken ct) =>
        {
            if (!Local(http)) return Results.NotFound();
            var result = await service.GetAsync(Token(http), taxpayerId, invoiceId, ct);
            if (result.Decision == InvoiceApiDecision.Unauthenticated)
                return Results.Unauthorized();
            if (result.Decision == InvoiceApiDecision.Forbidden)
                return Results.StatusCode(403);
            if (result.Decision != InvoiceApiDecision.Success)
                return Results.NotFound();
            if (configuration["SandboxInvoiceLegacy:Enabled"] != "true" ||
                configuration["SandboxInvoiceLegacy:OperatorApprovedEndpoint"] != "true")
                return Results.Json(new { error = "invoice_dispatch_not_enabled",
                    environment = "Sandbox", submissionEnabled = false,
                    fiscalAccepted = false }, statusCode: StatusCodes.Status503ServiceUnavailable);
            var sent = await worker.ProcessInvoiceAsync(taxpayerId, invoiceId, ct);
            return sent.Status switch
            {
                "Acknowledged" => Results.Ok(new { environment = "Sandbox",
                    submissionEnabled = true, fiscalAccepted = false,
                    status = sent.Status, safeCode = sent.SafeCode,
                    referenceNumber = sent.ReferenceNumber, attemptId = sent.AttemptId }),
                "Indeterminate" => Results.Conflict(new { environment = "Sandbox",
                    submissionEnabled = true, fiscalAccepted = false,
                    status = sent.Status, safeCode = sent.SafeCode,
                    attemptId = sent.AttemptId }),
                "Blocked" => Results.Conflict(new { environment = "Sandbox",
                    submissionEnabled = false, fiscalAccepted = false,
                    status = sent.Status, safeCode = sent.SafeCode }),
                _ => Results.NotFound(new { error = sent.SafeCode })
            };
        });
        return routes;
    }

    private static bool Local(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        return ip is not null && (IPAddress.IsLoopback(ip) ||
            ip.IsIPv4MappedToIPv6 && IPAddress.IsLoopback(ip.MapToIPv4()));
    }

    private static string? Token(HttpContext http)
    {
        var header = http.Request.Headers.Authorization.ToString();
        return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header[7..].Trim() : null;
    }
}
