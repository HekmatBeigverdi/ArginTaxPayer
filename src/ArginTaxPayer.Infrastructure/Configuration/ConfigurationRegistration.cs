using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ArginTaxPayer.Infrastructure.Configuration;

/// <summary>Shared configuration contract for API and Workers; no connection strings or keys here.</summary>
public static class ConfigurationRegistration
{
    public static IServiceCollection AddArginTaxPayerConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<PlatformOptions>>(
            new PlatformOptionsValidator(isProduction)));
        services.AddOptions<PlatformOptions>()
            .Bind(configuration.GetSection(PlatformOptions.SectionName))
            .ValidateOnStart();

        return services;
    }
}
