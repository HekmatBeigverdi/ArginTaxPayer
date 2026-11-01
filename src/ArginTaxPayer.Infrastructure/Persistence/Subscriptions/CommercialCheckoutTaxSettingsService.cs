using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;
public sealed class CommercialCheckoutTaxSettingsService(
 ArginTaxPayerDbContext db,IdentityAuthorizationService identity)
{
 public async Task<int?> ReadAsync(string? bearer,CancellationToken ct=default)
 {
  var actor=await RequireRootAsync(bearer,ct);
  var row=await db.Database.SqlQueryRaw<RateRow>(
    """SELECT (value_json->>'basis_points')::integer AS "BasisPoints" FROM application_settings WHERE scope_user_id={0} AND setting_key='checkout.vat'""",actor)
    .SingleOrDefaultAsync(ct);
  return row?.BasisPoints;
 }
 public async Task SaveAsync(string? bearer,int basisPoints,CancellationToken ct=default)
 {
  if(basisPoints is <0 or >10000)throw new ArgumentOutOfRangeException(nameof(basisPoints));
  var actor=await RequireRootAsync(bearer,ct);
  await db.Database.ExecuteSqlInterpolatedAsync($"""
    INSERT INTO application_settings
      (scope_user_id,setting_key,value_json,updated_by_user_id,updated_at_utc)
    VALUES({actor},'checkout.vat',jsonb_build_object('basis_points',{basisPoints}),{actor},{DateTime.UtcNow})
    ON CONFLICT(scope_user_id,setting_key) DO UPDATE
    SET value_json=EXCLUDED.value_json,
        updated_by_user_id=EXCLUDED.updated_by_user_id,
        updated_at_utc=EXCLUDED.updated_at_utc,
        revision=application_settings.revision+1
    """,ct);
 }
 private async Task<Guid> RequireRootAsync(string? bearer,CancellationToken ct)
 {
  var actor=await identity.CurrentUserIdAsync(bearer,ct)
    ?? throw new UnauthorizedAccessException("Authentication required.");
  var ok=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor && u.IsActive &&
    u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1) &&
    db.IdentityMfaStates.Any(m=>m.UserId==actor && m.EnabledAtUtc!=null),ct);
  if(!ok)throw new UnauthorizedAccessException("Level1 MFA required.");
  return actor;
 }
 private sealed class RateRow {public int BasisPoints{get;set;}}
}
