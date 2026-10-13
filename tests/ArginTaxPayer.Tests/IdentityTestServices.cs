using ArginTaxPayer.Infrastructure.Persistence;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.Extensions.Configuration;

namespace ArginTaxPayer.Tests;

internal static class IdentityTestServices
{
    public static CustomerCommercialOwnershipService CreateOwnership(
        ArginTaxPayerDbContext db,
        IdentitySessionService? sessions = null,
        IConfiguration? configuration = null)
    {
        var sessionService = sessions ?? new IdentitySessionService(db);
        return new CustomerCommercialOwnershipService(
            db,
            new IdentityAuthorizationService(db, sessionService),
            configuration ?? new ConfigurationBuilder().Build());
    }
}
