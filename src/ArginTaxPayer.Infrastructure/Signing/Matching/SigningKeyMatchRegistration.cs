using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.Matching;

// Explicit future custody-host registration. Api/BFF do not invoke this.
public static class SigningKeyMatchRegistration
{
    public static IServiceCollection AddPhase09KeyMatching(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration["SigningKeyMatching:Enabled"] != "true" ||
            configuration["SigningKeyProtection:Enabled"] != "true")
        {
            services.AddSingleton<ISigningKeyMatchService>(_ => new UnavailableMatchService());
            return services;
        }
        services.AddPhase09KeyProtection(configuration);
        services.AddScoped<ISigningKeyMatchJournal>(sp => new EfSigningOperationRepository(
            sp.GetRequiredService<ArginTaxPayerDbContext>()));
        services.AddSingleton<SigningPublicKeyParser>(_ => new());
        services.AddScoped<IKeyMatchProof>(sp => new VaultKeyMatchProof(
            sp.GetRequiredService<IProtectedKeyEnvelopeStore>(), sp.GetRequiredService<KeyEnvelopeProtector>()));
        services.AddScoped<ISigningKeyMatchService>(sp => new SigningKeyMatchService(
            sp.GetRequiredService<ISigningKeyMatchJournal>(), sp.GetRequiredService<IKeyMatchProof>(),
            sp.GetRequiredService<SigningPublicKeyParser>()));
        return services;
    }
    private sealed class UnavailableMatchService : ISigningKeyMatchService
    {
        public Task<SigningKeyMatchResult> MatchAsync(string? bearer, MatchSigningKeyRequest request,
            ReadOnlyMemory<byte> providedPublicKey, CancellationToken ct = default) =>
            Task.FromResult(new SigningKeyMatchResult(SigningKeyMatchStatus.Unavailable));
    }
}
