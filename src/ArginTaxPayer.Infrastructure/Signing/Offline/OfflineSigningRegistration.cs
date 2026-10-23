using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.Offline;

/// <summary>Creates the internal engine only for explicit future custody-host composition.</summary>
public static class OfflineSigningRegistration
{
    public static IServiceCollection AddPhase09OfflineSigning(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration["SigningKeyOfflineSigning:Enabled"] == "true")
            services.AddSingleton<ILegacyOfflineSigningEngine, LegacyOfflineSigningEngine>();
        return services;
    }
}
