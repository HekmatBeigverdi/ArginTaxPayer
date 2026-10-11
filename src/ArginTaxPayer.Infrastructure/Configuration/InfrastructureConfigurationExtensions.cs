using ArginTaxPayer.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure;

/// <summary>Expose shared typed configuration from the Infrastructure composition surface.</summary>
public static class InfrastructureConfigurationExtensions
{
    public static IServiceCollection AddArginTaxPayerConfiguration(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction) =>
        ConfigurationRegistration.AddArginTaxPayerConfiguration(services, configuration, isProduction);
}
