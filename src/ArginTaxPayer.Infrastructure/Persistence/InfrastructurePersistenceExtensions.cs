using ArginTaxPayer.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure;

/// <summary>Public persistence composition entry point used by both application hosts.</summary>
public static class InfrastructurePersistenceExtensions
{
    public static IServiceCollection AddArginTaxPayerPersistence(
        this IServiceCollection services, IConfiguration configuration, bool isProduction) =>
        PersistenceRegistration.AddArginTaxPayerPersistence(services, configuration, isProduction);
}
