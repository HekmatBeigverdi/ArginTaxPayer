using ArginTaxPayer.Application.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ArginTaxPayer.Infrastructure.Signing.Lifecycle;

/// <summary>Future host opt-in; the current API/BFF does not expose lifecycle commands.</summary>
public static class SigningKeyLifecycleRegistration
{
    public static IServiceCollection AddPhase09SigningKeyLifecycle(this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration["SigningKeyLifecycle:Enabled"] == "true")
            services.AddScoped<ISigningKeyLifecycleService, SigningKeyLifecycleService>();
        return services;
    }
}
