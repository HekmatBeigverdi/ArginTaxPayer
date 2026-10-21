using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.Generation;

/// <summary>Future custody-host opt-in only. Api/BFF do not invoke this registration in Step06.</summary>
public static class SigningKeyGenerationRegistration
{
    public static IServiceCollection AddPhase09KeyGeneration(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration["SigningKeyGeneration:Enabled"] != "true" ||
            configuration["SigningKeyProtection:Enabled"] != "true")
        {
            services.AddScoped<ISigningKeyGenerationService>(_ => new UnavailableGenerationService());
            return services;
        }
        services.AddPhase09KeyProtection(configuration);
        services.AddScoped<ISigningGenerationJournal>(sp =>
            new EfSigningOperationRepository(sp.GetRequiredService<ArginTaxPayerDbContext>()));
        services.AddSingleton<IGeneratedKeyFactory>(_ => new RsaGeneratedKeyFactory());
        services.AddScoped<IGeneratedKeyCustody>(sp => new VaultGeneratedKeyCustody(
            sp.GetRequiredService<ProtectedKeyEnvelopeStore>(), sp.GetRequiredService<KeyEnvelopeProtector>(),
            sp.GetRequiredService<VaultHttpTransport>(), sp.GetRequiredService<VaultProtectionOptions>()));
        services.AddScoped<ISigningKeyGenerationService>(sp => new SigningKeyGenerationService(
            sp.GetRequiredService<ISigningGenerationJournal>(), sp.GetRequiredService<IGeneratedKeyCustody>(),
            sp.GetRequiredService<IGeneratedKeyFactory>()));
        return services;
    }

    private sealed class UnavailableGenerationService : ISigningKeyGenerationService
    {
        public Task<SigningKeyGenerationResult> GenerateAsync(string? bearer, GenerateSigningKeyRequest request,
            CancellationToken ct = default) => Task.FromResult(new SigningKeyGenerationResult(SigningGenerationStatus.Unavailable));
        public Task<SigningKeyGenerationResult> ReadPublicAsync(string? bearer, Guid taxpayerId, Guid fiscalMemoryId,
            Guid keyVersionId, CancellationToken ct = default) =>
            Task.FromResult(new SigningKeyGenerationResult(SigningGenerationStatus.Unavailable));
    }
}
