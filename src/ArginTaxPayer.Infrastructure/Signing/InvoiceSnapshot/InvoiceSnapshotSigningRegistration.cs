using ArginTaxPayer.Application.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.InvoiceSnapshot;

/// <summary>Future custody-host opt-in. The current API/BFF deliberately does not call this.</summary>
public static class InvoiceSnapshotSigningRegistration
{
    public static IServiceCollection AddPhase10InvoiceSigning(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration["SigningKeyOfflineSigning:Enabled"] == "true" &&
            configuration["SigningKeyProtection:Enabled"] == "true")
            services.AddScoped<IDigitalSigningService, InvoiceSnapshotSigningService>();
        return services;
    }
}
