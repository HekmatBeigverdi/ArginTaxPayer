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
    """SELECT vat_basis_points AS "BasisPoints" FROM commercial_checkout_tax_settings WHERE root_user_id={0}""",actor)
    .SingleOrDefaultAsync(ct);
  return row?.BasisPoints;
 }
 public async Task SaveAsync(string? bearer,int basisPoints,CancellationToken ct=default)
 {
  if(basisPoints is <0 or >10000)throw new ArgumentOutOfRangeException(nameof(basisPoints));
  var actor=await RequireRootAsync(bearer,ct);
  await db.Database.ExecuteSqlInterpolatedAsync($"""
    INSERT INTO commercial_checkout_tax_settings(root_user_id,vat_basis_points,updated_by_user_id,updated_at_utc)
    VALUES({actor},{basisPoints},{actor},{DateTime.UtcNow})
    ON CONFLICT(root_user_id) DO UPDATE
    SET vat_basis_points=EXCLUDED.vat_basis_points,
        updated_by_user_id=EXCLUDED.updated_by_user_id,
        updated_at_utc=EXCLUDED.updated_at_utc
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
