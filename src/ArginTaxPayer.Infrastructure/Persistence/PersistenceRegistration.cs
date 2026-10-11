using ArginTaxPayer.Application.Abstractions.Persistence;
using ArginTaxPayer.Application.Abstractions.Messaging;
using ArginTaxPayer.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence;

/// <summary>
/// Register a scoped PostgreSQL DbContext when configured; fail closed for missing production configuration.
/// </summary>
public static class PersistenceRegistration
{
    public const string ConnectionName = "ArginTaxPayerDb";

    public static IServiceCollection AddArginTaxPayerPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (isProduction)
                throw new InvalidOperationException("The PostgreSQL connection must be configured in Production.");

            // Keep pre-existing Development/Test bootstrap hosts runnable without PostgreSQL.
            return services;
        }

        // Parsing verifies basic connection-string syntax; never expose its full value in errors/logs.
        NpgsqlConnectionStringBuilder parsed;
        try
        {
            parsed = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("The PostgreSQL connection configuration is invalid.");
        }

        if (string.IsNullOrWhiteSpace(parsed.Host) ||
            string.IsNullOrWhiteSpace(parsed.Database) ||
            string.IsNullOrWhiteSpace(parsed.Username))
        {
            throw new InvalidOperationException("PostgreSQL host, database and username are required.");
        }

        if (isProduction && parsed.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
            throw new InvalidOperationException("Production PostgreSQL connections must require TLS.");

        services.AddDbContext<ArginTaxPayerDbContext>(options =>
            options.UseNpgsql(connectionString, postgres =>
            {
                postgres.CommandTimeout(30);
                postgres.EnableRetryOnFailure(maxRetryCount: 3);
            }));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IOutboxWriter, EfOutboxWriter>();

        return services;
    }
}
