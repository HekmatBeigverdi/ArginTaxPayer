using ArginTaxPayer.Application.Abstractions.Security;
using ArginTaxPayer.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArginTaxPayer.Infrastructure;

/// <summary>Infrastructure composition entry point; concrete adapters are added in later steps.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddArginTaxPayerInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // A harmless registration verifies this composition root is called by both hosts.
        // No database, credentials, external adapter, or hosted service is registered here.
        services.TryAddSingleton<AssemblyMarker>();
        services.TryAddSingleton<ISecretProvider>(_ =>
            new EnvironmentSecretProvider(Environment.GetEnvironmentVariable));
        return services;
    }
}
