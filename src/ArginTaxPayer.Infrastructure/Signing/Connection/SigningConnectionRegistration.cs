using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Application.Invoices;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

public static class SigningConnectionRegistration
{
    public static IServiceCollection AddPhase10SandboxConnection(this IServiceCollection services)
    {
        services.AddScoped<SandboxAuthorityTokenProbe>();
        services.AddScoped<LegacyInvoiceAccessTokenClient>();
        services.AddScoped<LegacySandboxServerEncryptionKeyClient>();
        services.AddScoped<LegacyInvoiceSubmissionHttpTransport>();
        services.AddScoped<LegacyInvoiceOutboxSendCoordinator>();
        services.AddScoped<DatabaseLegacySigningKeyLoader>();
        services.AddScoped<SandboxLegacySigningKeyLoader>(sp =>
            new SandboxLegacySigningKeyLoader(
                sp.GetRequiredService<ArginTaxPayerDbContext>(),
                sp.GetRequiredService<KeyEnvelopeProtector>()));
        return services;
    }

    public static bool SandboxConnectionConfigured(IConfiguration config) =>
        SandboxAuthorityTokenProbe.TryResolveBaseUrl(config, out _);

    public static IServiceCollection AddPhase09AuthorityConnection(
        this IServiceCollection services, IConfiguration config)
    {
        // Same read-only Phase09 provider reused by Phase10; this never enables invoice dispatch.
        services.AddScoped<IInvoiceAuthorityAuthentication, Phase09InvoiceAuthorityAuthentication>();
        // Explicit opt-in: both existing-key and newly-generated key custody use this one adapter.
        if (config["SigningConnectionTest:Enabled"] != "true" ||
            config["SigningKeyProtection:Enabled"] != "true" ||
            config["SigningKeyImport:Enabled"] != "true" &&
            config["SigningKeyGeneration:Enabled"] != "true")
        {
            services.AddScoped<ISigningReadOnlyConnectionTest, UnavailableSigningReadOnlyConnectionTest>();
            return services;
        }
        services.AddScoped<ISigningReadOnlyConnectionTest>(sp => new LegacyTaxAuthorityConnection(
            sp.GetRequiredService<ArginTaxPayerDbContext>(),
            sp.GetRequiredService<EfSigningOperationRepository>(),
            sp.GetRequiredService<ProtectedKeyEnvelopeStore>(),
            sp.GetRequiredService<KeyEnvelopeProtector>(),
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("phase09-authority"),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LegacyTaxAuthorityConnection>>()));
        return services;
    }
}
