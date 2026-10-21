using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.Connection;

public static class SigningConnectionRegistration
{
    public static IServiceCollection AddPhase09AuthorityConnection(
        this IServiceCollection services, IConfiguration config)
    {
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
            sp.GetRequiredService<IHttpClientFactory>().CreateClient("phase09-authority")));
        return services;
    }
}
