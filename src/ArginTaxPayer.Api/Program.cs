using ArginTaxPayer.Api.Subscriptions;
using ArginTaxPayer.Api.Invoices;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using ArginTaxPayer.Api.Health;
using ArginTaxPayer.Api.Identity;
using ArginTaxPayer.Api.Taxpayers;
using ArginTaxPayer.Api.Parties;
using ArginTaxPayer.Api.Customers;
using ArginTaxPayer.Api.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Catalog;
using ArginTaxPayer.Infrastructure.Persistence.Customers;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Api.Observability;
using ArginTaxPayer.Application;
using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Signing.Import;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Connection;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using ArginTaxPayer.Infrastructure.Signing.Review;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var identityDevelopmentEnabled = builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("ArginTaxPayerDb"));
// JSON console output is machine-readable on macOS, Ubuntu and future containers.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
builder.Services.AddArginTaxPayerApplication();
builder.Services.AddArginTaxPayerInfrastructure();
builder.Services.AddArginTaxPayerConfiguration(builder.Configuration, builder.Environment.IsProduction());
builder.Services.AddArginTaxPayerPersistence(builder.Configuration, builder.Environment.IsProduction());
// Explicit opt-in; observation only, never invoice sending or authority inquiry.
if (identityDevelopmentEnabled &&
    builder.Configuration.GetValue<bool>("Phase11:EnableReadOnlyQueueObserver"))
    builder.Services.AddHostedService<Phase11InquiryQueueObserver>();

// OpenAPI document generation is registered for API contract development.
builder.Services.AddOpenApi();
if (identityDevelopmentEnabled)
{
    builder.Services.AddPhase09KeyImport(builder.Configuration);
    builder.Services.AddPhase09KeyGeneration(builder.Configuration);
    // Pin a single TLS-validated legacy authority origin; never follow redirects or forward cookies.
    builder.Services.AddHttpClient("phase09-authority", client =>
        client.Timeout = TimeSpan.FromSeconds(18))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        { AllowAutoRedirect = false, UseCookies = false });
    builder.Services.AddPhase09AuthorityConnection(builder.Configuration);
    builder.Services.AddHttpClient("phase10-sandbox-authority", client =>
        client.Timeout = TimeSpan.FromSeconds(18))
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        { AllowAutoRedirect = false, UseCookies = false });
    builder.Services.AddPhase09KeyProtection(builder.Configuration);
    builder.Services.AddPhase10SandboxConnection();
    builder.Services.AddScoped<EfSigningOperationRepository>();
    builder.Services.AddScoped<ISigningChangeRequestSubmission, EfSigningChangeRequestSubmission>();
    builder.Services.AddScoped<ISigningAdminReviewWorkflow, EfSigningAdminReviewWorkflow>();
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = 429;
        options.OnRejected = (context, _) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return ValueTask.CompletedTask;
        };
        options.AddPolicy("signing-import", http => RateLimitPartition.GetFixedWindowLimiter(
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"{http.Request.Headers.Authorization}|{http.Request.Method}|{http.Request.Path}"))), _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    });
    builder.Services.AddSingleton<IdentityProtectionService>();
    builder.Services.AddScoped<IdentityCredentialService>();
    builder.Services.AddScoped<IdentityAdministratorUserProvisioning>();
    builder.Services.AddScoped<IdentitySessionService>();
    if (builder.Configuration["Identity:OtpDeliveryMode"] == "LocalFile")
        builder.Services.AddScoped<IIdentityOtpSender, LocalFileOtpSender>();
    else
        builder.Services.AddHttpClient<IIdentityOtpSender, GhasedakOtpSender>(client =>
            client.Timeout = TimeSpan.FromSeconds(12));
    builder.Services.AddScoped<IdentityPhoneService>();
    builder.Services.AddScoped<IdentityAuthorizationService>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Subscriptions.SubscriptionCatalogRepository>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Subscriptions.SubscriptionCreditLedgerService>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Subscriptions.CommercialOrderService>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Subscriptions.VerifiedCollectionService>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Subscriptions.PartnerRevenueAllocationService>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Subscriptions.CommercialReceivableReadService>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Subscriptions.SubscriptionPlanAdministrationService>();
    builder.Services.AddScoped<CustomerSelfServiceService>();
    builder.Services.AddScoped<SoftwareUserProfileService>();
    builder.Services.AddScoped<CustomerTaxpayerMembershipService>();
    builder.Services.AddScoped<CustomerAccountProfileService>();
    builder.Services.AddScoped<ParticipantScopeService>();
    builder.Services.AddScoped<CustomerProductService>();
    builder.Services.AddScoped<CustomerServiceCatalogService>();
    builder.Services.AddScoped<TaxIdentifierMappingService>();
    builder.Services.AddScoped<CatalogSearchService>();
    builder.Services.AddScoped<PartyIdentityEditService>();
    builder.Services.AddScoped<InvoiceParticipantDetailsService>();
    builder.Services.AddScoped<FiscalParticipantDirectoryService>();
    builder.Services.AddScoped<PartyDirectoryService>();
    builder.Services.AddScoped<NaturalPersonService>();
    builder.Services.AddScoped<LegalEntityService>();
    builder.Services.AddScoped<PartyContactAddressService>();
    builder.Services.AddScoped<PartyDeduplicationService>();
    builder.Services.AddScoped<TaxpayerCreationService>();
    builder.Services.AddScoped<TaxpayerProfileLifecycleService>();
    builder.Services.AddScoped<FiscalMemoryManagementService>();
    builder.Services.AddScoped<CustomerCommercialOwnershipService>();
    builder.Services.AddScoped<TaxpayerScopeService>();
    builder.Services.AddScoped<InvoiceDraftApplicationService>();
    builder.Services.AddScoped<InvoiceReviewCommandService>();
    builder.Services.AddScoped<Phase10ReviewReadService>();
    builder.Services.AddScoped<Phase10InternalInvoiceValidationService>();
    builder.Services.AddScoped<Phase10InvoiceFinalizationService>();
    builder.Services.AddScoped<Phase10LegacyInvoicePreparationService>();
    builder.Services.AddScoped<Phase10InvoiceDispatchReadService>();
    builder.Services.AddScoped<Phase10InvoicePrintPreviewService>();
    builder.Services.AddScoped<Phase10OneClickSandboxSubmissionService>();
    builder.Services.AddScoped<Phase10InvoiceLineageReadService>();
    builder.Services.AddScoped<Phase10CancellationEligibilityService>();
    builder.Services.AddScoped<LegacyAuthorityReportRepository>();
    builder.Services.AddScoped<Phase10SandboxCancellationService>();
    builder.Services.AddScoped<Phase10SandboxCorrectionService>();
    builder.Services.AddScoped<Phase10SandboxSalesReturnService>();
    builder.Services.AddScoped<Phase10DispatchEvidenceRepository>();
    builder.Services.AddScoped<Phase10LegacyUidInquiryService>();
    builder.Services.AddScoped<Phase10LegacyReferenceInquiryService>();
    builder.Services.AddScoped<LegacyInvoiceCorrectionDraftService>();
    builder.Services.AddScoped<Phase10DispatchArmService>();
    builder.Services.AddScoped<Phase10LiveSendReadinessService>();
    builder.Services.AddScoped<Phase10InvoiceCalculationAuditService>();
    builder.Services.AddScoped<Phase10SandboxSubmissionAdmissionService>();
    builder.Services.AddScoped<Phase10InvoiceDispatchHistoryService>();
    builder.Services.AddScoped<ArginTaxPayer.Infrastructure.Persistence.Signing.SandboxFiscalMemoryMetadataService>();
    builder.Services.AddScoped<InvoiceEntryWorkspaceService>();
    builder.Services.AddScoped<InvoiceWorkspacePromotionService>();
    builder.Services.AddScoped<InvoiceValidationReferenceService>();
    builder.Services.AddScoped<FiscalCertificateMetadataService>();
    builder.Services.AddScoped<IIdentityResourceOwnershipResolver, IdentityResourceOwnershipResolver>();
    builder.Services.AddScoped<IdentityResourceAuthorizationService>();
    var mfaEncryptionKey = builder.Configuration["Identity:MfaEncryptionKey"];
    if (!string.IsNullOrWhiteSpace(mfaEncryptionKey))
    {
        builder.Services.AddSingleton(new IdentityMfaCrypto(mfaEncryptionKey));
        builder.Services.AddScoped<IdentityMfaService>();
    }
}

// Separate cheap process liveness from real PostgreSQL readiness.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
if (identityDevelopmentEnabled) app.UseRateLimiter();

// Never expose the API contract endpoint in Production by default.
if (app.Environment.IsDevelopment())
{
    // Diagnostic deliberately contains no configuration values, credentials or IPs.
    // Registered even when development identity endpoints were not mapped.
    app.MapGet("/health/dev-identity", (HttpContext http) =>
    {
        if (!IdentityDevelopmentEndpoints.IsLoopbackAddress(http.Connection.RemoteIpAddress))
            return Results.NotFound();
        return Results.Ok(new
        {
            development = true,
            connectionConfigured = !string.IsNullOrWhiteSpace(
                app.Configuration.GetConnectionString("ArginTaxPayerDb")),
            identityEndpointsRegistered = identityDevelopmentEnabled,
            registrationEnabled = app.Configuration.GetValue<bool>(
                "Identity:AllowDevelopmentRegistration"),
            smsSendingEnabled = app.Configuration.GetValue<bool>("Ghasedak:EnableLiveSending"),
            mfaConfigured = !string.IsNullOrWhiteSpace(app.Configuration["Identity:MfaEncryptionKey"]),
            ghasedakApiKeyConfigured = !string.IsNullOrWhiteSpace(app.Configuration["Ghasedak:ApiKey"]),
            ghasedakTemplateConfigured = app.Configuration["Ghasedak:TemplateName"] == "TaxPayerActivationCode",
            ghasedakLoginTemplateConfigured = true,
            ghasedakRecipientRestrictionEnabled = app.Configuration.GetValue("Ghasedak:RestrictToTestRecipient", true),
            ghasedakTestRecipientConfigured = ArginTaxPayer.Infrastructure.Persistence.Identity.IranianMobileNumber.TryNormalize(
                app.Configuration["Ghasedak:TestRecipientMobile"], out _),
            otpPepperConfigured = (app.Configuration["Identity:OtpPepper"]?.Length ?? 0) >= 32,
            localOtpTestMode = app.Configuration["Identity:OtpDeliveryMode"] == "LocalFile",
            // Flags are read from the RUNNING API process, not the shell .env file.
            // Expose no hostnames, paths, Vault tokens, secrets, PEMs or credentials.
            signingKeyProtectionEnabled = app.Configuration.GetValue<bool>("SigningKeyProtection:Enabled"),
            signingKeyImportEnabled = app.Configuration.GetValue<bool>("SigningKeyImport:Enabled"),
            signingKeyGenerationEnabled = app.Configuration.GetValue<bool>("SigningKeyGeneration:Enabled"),
            signingConnectionTestEnabled = app.Configuration.GetValue<bool>("SigningConnectionTest:Enabled"),
            loopbackRecognized = true
        });
    }).ExcludeFromDescription();
    app.MapOpenApi();
    if (identityDevelopmentEnabled)
    {
        app.MapIdentityDevelopmentEndpoints();
        app.MapSoftwareUserProfileEndpoints();
        app.MapTaxpayerDevelopmentEndpoints();
        app.MapSigningImportEndpoints();
        app.MapSigningImportRecoveryEndpoints();
        app.MapSigningGenerationEndpoints();
        app.MapSigningConnectionEndpoints();
        app.MapSandboxFiscalMemoryDevelopmentEndpoints();
        app.MapPhase12PlanAdministrationEndpoints();
        app.MapPhase12CommercialOrderEndpoints();
        app.MapPhase12CollectionEndpoints();
        app.MapSigningChangeRequestEndpoints();
        app.MapSigningAdminReviewEndpoints();
        app.MapPartyDevelopmentEndpoints();
        app.MapFiscalParticipantDirectoryEndpoints();
        app.MapInvoiceParticipantEndpoints();
        app.MapInvoiceDraftDevelopmentEndpoints();
        app.MapInvoiceReviewDevelopmentEndpoints();
        app.MapPhase10InvoiceDispatchDevelopmentEndpoints();
        app.MapCustomerProfileDevelopmentEndpoints();
        app.MapCustomerTaxpayerMembershipEndpoints();
        app.MapCustomerSelfServiceEndpoints();
        app.MapCatalogDevelopmentEndpoints();
    }
}

// Preserve the existing bootstrap endpoint.
app.MapGet("/", () => Results.Ok(new { application = "ArginTaxPayer", stage = "bootstrap" }))
    .WithName("GetBootstrapStatus")
    .WithTags("Platform");

// Minimal responses by design: never return raw connection strings or exceptions.
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    return JsonSerializer.SerializeAsync(context.Response.Body,
        new { status = report.Status.ToString() },
        cancellationToken: context.RequestAborted);
}

static async Task RunHealthCheckAsync(
    HttpContext context,
    HealthCheckService healthCheckService,
    string tag)
{
    var report = await healthCheckService.CheckHealthAsync(
        registration => registration.Tags.Contains(tag),
        context.RequestAborted);
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;
    await WriteHealthResponse(context, report);
}

app.MapGet("/health/live", (HttpContext context, HealthCheckService healthChecks) =>
        RunHealthCheckAsync(context, healthChecks, "live"))
    .WithName("GetLiveness");

app.MapGet("/health/ready", (HttpContext context, HealthCheckService healthChecks) =>
        RunHealthCheckAsync(context, healthChecks, "ready"))
    .WithName("GetReadiness");

app.Run();

public partial class Program { }
