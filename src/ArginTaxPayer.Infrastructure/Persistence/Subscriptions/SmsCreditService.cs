using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record SmsCreditView(bool IsUnlimited, long? GrantedIrr,
    long? SpentIrr, long? ReservedIrr, long? AvailableIrr);

/// <summary>Money-denominated SMS credit. Legacy unit wallet history is separate.</summary>
public sealed class SmsCreditService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    public const long InitialCustomerCreditIrr = 2_000_000;
    private const string InitialCreditOperation = "welcome-sms-credit-v1";

    /// <summary>
    /// Must run inside the caller's account-creation transaction, after the
    /// Customer identity and role have been inserted.
    /// </summary>
    public async Task GrantInitialCustomerCreditWithinTransactionAsync(Guid customerUserId,
        DateTime createdAtUtc, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Initial SMS credit must share the customer creation transaction.");
        if (customerUserId == Guid.Empty || createdAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invalid initial SMS credit request.");

        var isCustomer = await db.IdentityUsers.AsNoTracking().AnyAsync(user =>
            user.Id == customerUserId && user.IsActive &&
            user.Roles.Any(role => role.Role == IdentityRoleNames.Customer) &&
            !user.Roles.Any(role => role.Role == IdentityRoleNames.Level1 ||
                role.Role == IdentityRoleNames.Level2), ct);
        if (!isCustomer)
            throw new UnauthorizedAccessException("SMS credit is for Customer accounts only.");

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sms_credit_accounts(customer_user_id,granted_irr,spent_irr,
                reserved_irr,revision,updated_at_utc)
            VALUES({customerUserId},0,0,0,1,{createdAtUtc})
            ON CONFLICT(customer_user_id) DO NOTHING
            """, ct);

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sms_credit_events(id,customer_user_id,operation_key,event_kind,
                granted_delta_irr,spent_delta_irr,reserved_delta_irr,dispatch_id,
                reference,recorded_at_utc)
            VALUES({Guid.NewGuid()},{customerUserId},{InitialCreditOperation},1,
                {InitialCustomerCreditIrr},0,0,NULL,'initial-customer-sms-credit',{createdAtUtc})
            ON CONFLICT(customer_user_id,operation_key) DO NOTHING
            """, ct);

        if (inserted == 0) return;
        var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE sms_credit_accounts SET granted_irr=granted_irr+{InitialCustomerCreditIrr},
                revision=revision+1,updated_at_utc={createdAtUtc}
            WHERE customer_user_id={customerUserId}
            """, ct);
        if (updated != 1)
            throw new InvalidOperationException("Initial SMS credit account update failed.");
    }

    public async Task<SmsCreditView> ReadMineAsync(string? bearer,
        CancellationToken ct = default)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var roles = await db.IdentityUsers.AsNoTracking().Where(user =>
                user.Id == actor && user.IsActive)
            .Select(user => new
            {
                IsCustomer = user.Roles.Any(role => role.Role == IdentityRoleNames.Customer),
                IsAdministrator = user.Roles.Any(role => role.Role == IdentityRoleNames.Level1 ||
                    role.Role == IdentityRoleNames.Level2)
            }).SingleOrDefaultAsync(ct);
        if (roles is null) throw new UnauthorizedAccessException("Active account required.");
        if (roles.IsAdministrator) return new SmsCreditView(true, null, null, null, null);
        if (!roles.IsCustomer) throw new UnauthorizedAccessException("Customer account required.");

        var row = await db.Database.SqlQueryRaw<SmsCreditBalanceRow>("""
            SELECT granted_irr AS "GrantedIrr", spent_irr AS "SpentIrr",
                   reserved_irr AS "ReservedIrr"
            FROM sms_credit_accounts WHERE customer_user_id={0}
            """, actor).SingleOrDefaultAsync(ct);
        var balance = row is null ? new SmsCreditBalance(0, 0, 0) :
            new SmsCreditBalance(row.GrantedIrr, row.SpentIrr, row.ReservedIrr);
        balance.Validate();
        return new SmsCreditView(false, balance.GrantedIrr, balance.SpentIrr,
            balance.ReservedIrr, balance.AvailableIrr);
    }

    internal async Task<SmsCreditBalance> ReadCustomerBalanceAsync(Guid customerUserId,
        CancellationToken ct = default)
    {
        var row = await db.Database.SqlQueryRaw<SmsCreditBalanceRow>("""
            SELECT granted_irr AS "GrantedIrr", spent_irr AS "SpentIrr",
                   reserved_irr AS "ReservedIrr"
            FROM sms_credit_accounts WHERE customer_user_id={0}
            """, customerUserId).SingleOrDefaultAsync(ct);
        var balance = row is null ? new SmsCreditBalance(0, 0, 0) :
            new SmsCreditBalance(row.GrantedIrr, row.SpentIrr, row.ReservedIrr);
        balance.Validate();
        return balance;
    }

    private sealed class SmsCreditBalanceRow
    {
        public long GrantedIrr { get; set; }
        public long SpentIrr { get; set; }
        public long ReservedIrr { get; set; }
    }
}
