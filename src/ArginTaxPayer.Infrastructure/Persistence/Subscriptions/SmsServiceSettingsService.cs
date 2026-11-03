using System.Data;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record SmsServiceSettings(int ServiceFeeBasisPoints,
    long ReservationCeilingIrr, bool CustomerBillingEnabled,
    long Revision, DateTime UpdatedAtUtc);

public sealed class SmsServiceSettingsService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity)
{
    internal async Task<SmsServiceSettings?> ReadForRootAsync(Guid root, CancellationToken ct = default)
    {
        return await db.Database.SqlQueryRaw<SmsServiceSettingsRow>("""
            SELECT (value_json->>'service_fee_basis_points')::integer AS "ServiceFeeBasisPoints",
                   (value_json->>'reservation_ceiling_irr')::bigint AS "ReservationCeilingIrr",
                   CASE WHEN jsonb_typeof(value_json->'customer_billing_enabled')='boolean'
                     THEN (value_json->>'customer_billing_enabled')::boolean ELSE TRUE END AS "CustomerBillingEnabled",
                   revision AS "Revision", updated_at_utc AS "UpdatedAtUtc"
            FROM application_settings WHERE scope_user_id={0} AND setting_key='sms.service'
            """, root).Select(x => new SmsServiceSettings(x.ServiceFeeBasisPoints,
                x.ReservationCeilingIrr, x.CustomerBillingEnabled, x.Revision, x.UpdatedAtUtc)).SingleOrDefaultAsync(ct);
    }

    public async Task<SmsServiceSettings?> GetAsync(string? bearer,
        CancellationToken ct = default)
    {
        var root = await RequireRootAsync(bearer, ct);
        return await db.Database.SqlQueryRaw<SmsServiceSettingsRow>("""
            SELECT (value_json->>'service_fee_basis_points')::integer AS "ServiceFeeBasisPoints",
                   (value_json->>'reservation_ceiling_irr')::bigint AS "ReservationCeilingIrr",
                   CASE WHEN jsonb_typeof(value_json->'customer_billing_enabled')='boolean'
                     THEN (value_json->>'customer_billing_enabled')::boolean ELSE TRUE END AS "CustomerBillingEnabled",
                   revision AS "Revision", updated_at_utc AS "UpdatedAtUtc"
            FROM application_settings
            WHERE scope_user_id={0} AND setting_key='sms.service'
            """, root).Select(x => new SmsServiceSettings(x.ServiceFeeBasisPoints,
                x.ReservationCeilingIrr, x.CustomerBillingEnabled, x.Revision, x.UpdatedAtUtc))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<SmsServiceSettings> SaveAsync(string? bearer,
        int serviceFeeBasisPoints, long reservationCeilingIrr, bool customerBillingEnabled,
        CancellationToken ct = default)
    {
        if (serviceFeeBasisPoints is < 0 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(serviceFeeBasisPoints));
        if (reservationCeilingIrr <= 0)
            throw new ArgumentOutOfRangeException(nameof(reservationCeilingIrr));

        var root = await RequireRootAsync(bearer, ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, ct);
            var now = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO application_settings(
                  scope_user_id,setting_key,value_json,updated_by_user_id,updated_at_utc)
                VALUES({root},'sms.service',jsonb_build_object(
                  'service_fee_basis_points',{serviceFeeBasisPoints},'reservation_ceiling_irr',{reservationCeilingIrr},
                  'customer_billing_enabled',{customerBillingEnabled}),{root},{now})
                ON CONFLICT(scope_user_id,setting_key) DO UPDATE SET
                  value_json=EXCLUDED.value_json,
                  revision=application_settings.revision+1,
                  updated_by_user_id=EXCLUDED.updated_by_user_id,
                  updated_at_utc=EXCLUDED.updated_at_utc
                """, ct);
            var row = await db.Database.SqlQueryRaw<SmsServiceSettingsRow>("""
                SELECT (value_json->>'service_fee_basis_points')::integer AS "ServiceFeeBasisPoints",
                  (value_json->>'reservation_ceiling_irr')::bigint AS "ReservationCeilingIrr",
                  CASE WHEN jsonb_typeof(value_json->'customer_billing_enabled')='boolean'
                    THEN (value_json->>'customer_billing_enabled')::boolean ELSE TRUE END AS "CustomerBillingEnabled",
                  revision AS "Revision",updated_at_utc AS "UpdatedAtUtc"
                FROM application_settings WHERE scope_user_id={0} AND setting_key='sms.service'
                """,root).SingleAsync(ct);

            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO sms_service_setting_events(
                  id,root_level1_user_id,revision,service_fee_basis_points,
                  reservation_ceiling_irr,customer_billing_enabled,recorded_by_user_id,recorded_at_utc)
                VALUES({0},{1},{2},{3},{4},{5},{1},{6})
                """, [Guid.NewGuid(), root, row.Revision, row.ServiceFeeBasisPoints,
                    row.ReservationCeilingIrr, row.CustomerBillingEnabled, row.UpdatedAtUtc], ct);
            await transaction.CommitAsync(ct);
            return new SmsServiceSettings(row.ServiceFeeBasisPoints,
                row.ReservationCeilingIrr, row.CustomerBillingEnabled, row.Revision, row.UpdatedAtUtc);
        });
    }

    private async Task<Guid> RequireRootAsync(string? bearer, CancellationToken ct)
    {
        var actor = await identity.CurrentUserIdAsync(bearer, ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        var authorized = await db.IdentityUsers.AsNoTracking().AnyAsync(user =>
            user.Id == actor && user.IsActive &&
            user.Roles.Any(role => role.Role == IdentityRoleNames.Level1) &&
            db.IdentityMfaStates.Any(state => state.UserId == actor &&
                state.EnabledAtUtc != null), ct);
        if (!authorized)
            throw new UnauthorizedAccessException("Level 1 MFA required.");
        return actor;
    }

    private sealed class SmsServiceSettingsRow
    {
        public int ServiceFeeBasisPoints { get; set; }
        public long ReservationCeilingIrr { get; set; }
        public bool CustomerBillingEnabled { get; set; }
        public long Revision { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
