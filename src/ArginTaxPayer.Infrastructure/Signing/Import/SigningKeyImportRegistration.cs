using ArginTaxPayer.Application.Signing;
using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Signing;
using ArginTaxPayer.Infrastructure.Signing.Generation;
using ArginTaxPayer.Infrastructure.Signing.Protection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.Import;

public static class SigningKeyImportRegistration
{
    // Future custody-host opt-in. No Api/BFF call is added in Step07.
    public static IServiceCollection AddPhase09KeyImport(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration["SigningKeyImport:Enabled"] != "true" ||
            configuration["SigningKeyProtection:Enabled"] != "true")
        {
            services.AddScoped<ISigningKeyImportService>(_ => new UnavailableImportService());
            services.AddScoped<ISigningImportRecovery>(_ => new UnavailableRecovery());
            return services;
        }
        services.AddPhase09KeyProtection(configuration);
        services.AddScoped<ISigningImportJournal>(sp =>
            new EfSigningOperationRepository(sp.GetRequiredService<ArginTaxPayerDbContext>()));
        services.AddSingleton<Pkcs8SigningKeyParser>(_ => new());
        services.AddScoped<IGeneratedKeyCustody>(sp =>
            configuration["SigningKeyProtection:Provider"] == "Database"
                ? new DatabaseGeneratedKeyCustody(
                    sp.GetRequiredService<ProtectedKeyEnvelopeStore>(),
                    sp.GetRequiredService<KeyEnvelopeProtector>(),
                    sp.GetRequiredService<ArginTaxPayerDbContext>())
                : new VaultGeneratedKeyCustody(
                    sp.GetRequiredService<ProtectedKeyEnvelopeStore>(),
                    sp.GetRequiredService<KeyEnvelopeProtector>(),
                    sp.GetRequiredService<VaultHttpTransport>(),
                    sp.GetRequiredService<VaultProtectionOptions>()));
        if (configuration["SigningKeyProtection:Provider"] == "Database")
            services.AddScoped<ISigningImportRecovery>(sp => new DatabaseSigningImportRecovery(
                sp.GetRequiredService<ArginTaxPayerDbContext>(),
                sp.GetRequiredService<EfSigningOperationRepository>(),
                sp.GetRequiredService<IGeneratedKeyCustody>(),
                sp.GetRequiredService<ISigningImportJournal>()));
        else
            services.AddScoped<ISigningImportRecovery>(_ => new UnavailableRecovery());
        services.AddScoped<ISigningKeyImportService>(sp => new SigningKeyImportService(
            sp.GetRequiredService<ISigningImportJournal>(), sp.GetRequiredService<IGeneratedKeyCustody>(),
            sp.GetRequiredService<Pkcs8SigningKeyParser>()));
        return services;
    }

    private sealed class UnavailableRecovery : ISigningImportRecovery
    {
        public Task<SigningGenerationStatus> RecoverAsync(string? bearer, Guid taxpayerId,
            Guid fiscalMemoryId, CancellationToken ct = default)
            => Task.FromResult(SigningGenerationStatus.Unavailable);
    }

    private sealed class UnavailableImportService : ISigningKeyImportService
    {
        public Task<SigningKeyGenerationResult> ImportAsync(string? bearer, ImportSigningKeyRequest request,
            SensitiveKeyInput input, CancellationToken ct = default)
        {
            input?.Dispose();
            return Task.FromResult(new SigningKeyGenerationResult(SigningGenerationStatus.Unavailable));
        }
    }
}
