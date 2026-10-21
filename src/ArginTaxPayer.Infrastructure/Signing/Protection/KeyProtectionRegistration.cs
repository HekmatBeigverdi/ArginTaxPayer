using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.Protection;

/// <summary>Explicit opt-in for a future custody host. Not called by Api/BFF in Step05.</summary>
public static class KeyProtectionRegistration
{
    public static IServiceCollection AddPhase09KeyProtection(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection("SigningKeyProtection");
        if (section["Enabled"] != "true")
        {
            services.AddSingleton<IKeyProtectionProvider>(_ => new UnavailableKeyProtectionProvider());
            services.AddSingleton<IProtectedKeyEnvelopeStore>(_ => new UnavailableEnvelopeStore());
        }
        else
        {
            if (!Uri.TryCreate(section["Address"], UriKind.Absolute, out var address))
                throw new KeyProtectionException(KeyProtectionFailure.Unavailable);
            var options = new VaultProtectionOptions(address, section["TransitMount"] ?? "",
                section["TransitKey"] ?? "", section["KvMount"] ?? "",
                section["AgentTokenFile"] ?? "", section["ServerVersion"] ?? "");
            options.Validate();
            services.AddSingleton(options);
            services.AddSingleton<IVaultWorkloadTokenSource>(new VaultAgentTokenSource(options.AgentTokenFile));
            services.AddSingleton<VaultHttpTransport>(sp => new(options, sp.GetRequiredService<IVaultWorkloadTokenSource>()));
            services.AddSingleton<IKeyProtectionProvider>(sp => new VaultKeyProtectionProvider(
                sp.GetRequiredService<VaultHttpTransport>(), options));
            services.AddSingleton<IProtectedKeyEnvelopeStore>(sp => new VaultProtectedKeyEnvelopeStore(
                sp.GetRequiredService<VaultHttpTransport>(), options));
        }
        services.AddSingleton<KeyEnvelopeProtector>(sp => new(sp.GetRequiredService<IKeyProtectionProvider>()));
        services.AddSingleton<ProtectedKeyEnvelopeStore>(sp => new(
            sp.GetRequiredService<KeyEnvelopeProtector>(), sp.GetRequiredService<IProtectedKeyEnvelopeStore>()));
        return services;
    }

    private sealed class UnavailableEnvelopeStore : IProtectedKeyEnvelopeStore
    {
        public Task<StoredKeyEnvelope?> FindAsync(KeyProtectionIdentity id, CancellationToken ct) =>
            Task.FromException<StoredKeyEnvelope?>(new KeyProtectionException(KeyProtectionFailure.Unavailable));
        public Task<StoredKeyEnvelope> CreateAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope e, CancellationToken ct) =>
            Task.FromException<StoredKeyEnvelope>(new KeyProtectionException(KeyProtectionFailure.Unavailable));
        public Task<StoredKeyEnvelope> ReplaceAsync(KeyProtectionIdentity id, ProtectedKeyEnvelope e,
            int expectedVersion, CancellationToken ct) =>
            Task.FromException<StoredKeyEnvelope>(new KeyProtectionException(KeyProtectionFailure.Unavailable));
    }
}
