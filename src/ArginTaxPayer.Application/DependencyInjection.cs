using ArginTaxPayer.Application.Modules;
using ArginTaxPayer.Application.Abstractions.Tenancy;
using ArginTaxPayer.Application.Tenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArginTaxPayer.Application;

/// <summary>Application-only registrations. Called by each process composition root.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddArginTaxPayerApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Descriptors are metadata, not executable module services or access-control policies.
        // TryAddEnumerable prevents duplicate registrations if composition is invoked repeatedly.
        foreach (var descriptor in ModuleCatalog.All)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IModuleDescriptor>(descriptor));
        }

        services.TryAddScoped<TaxpayerScope>();
        services.TryAddScoped<ITaxpayerScope>(sp => sp.GetRequiredService<TaxpayerScope>());
        services.TryAddScoped<ITaxpayerScopeInitializer>(sp => sp.GetRequiredService<TaxpayerScope>());

        return services;
    }
}
