using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public sealed record SmsSepSettings(int? TerminalNumber, Guid? AssignedAdminUserId,
    long Revision, DateTime? UpdatedAtUtc);
public sealed record SmsSepAdminOption(Guid UserId, string Display);

public sealed class SmsSepSettingsService(ArginTaxPayerDbContext db, IdentityAuthorizationService identity)
{
    public async Task<SmsSepSettings> GetAsync(string? bearer,CancellationToken ct=default)
    {
        var root=await RequireRootAsync(bearer,ct);
        return await ReadForRootAsync(root,ct)??new(null,null,0,null);
    }

    public async Task<SmsSepSettings> SaveAsync(string? bearer,int terminalNumber,Guid assignedAdminUserId,CancellationToken ct=default)
    {
        if(terminalNumber<=0||assignedAdminUserId==Guid.Empty)throw new ArgumentException("Invalid SEP terminal configuration.");
        var root=await RequireRootAsync(bearer,ct);
        var valid=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==assignedAdminUserId&&u.IsActive&&
            (u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1)&&u.Id==root ||
             u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)&&
             db.IdentityMfaStates.Any(m=>m.UserId==u.Id&&m.EnabledAtUtc!=null)&&
             db.CustomerCommercialOwners.Any(o=>o.RootLevel1UserId==root&&o.PartnerLevel2UserId==u.Id)),ct);
        if(!valid)throw new ArgumentException("Selected administrator is outside this root's active scope.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            var now=DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_sep_terminal_settings(root_level1_user_id,terminal_number,assigned_admin_user_id,revision,updated_by_user_id,updated_at_utc)
                VALUES({root},{terminalNumber},{assignedAdminUserId},1,{root},{now})
                ON CONFLICT(root_level1_user_id) DO UPDATE SET terminal_number=EXCLUDED.terminal_number,
                  assigned_admin_user_id=EXCLUDED.assigned_admin_user_id,revision=sms_sep_terminal_settings.revision+1,
                  updated_by_user_id=EXCLUDED.updated_by_user_id,updated_at_utc=EXCLUDED.updated_at_utc
                """,ct);
            var current=await ReadForRootAsync(root,ct)??throw new InvalidOperationException("SEP setting write was not visible.");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_sep_terminal_setting_events(id,root_level1_user_id,terminal_number,assigned_admin_user_id,revision,changed_by_user_id,recorded_at_utc)
                VALUES({Guid.NewGuid()},{root},{terminalNumber},{assignedAdminUserId},{current.Revision},{root},{now})
                """,ct);
            await tx.CommitAsync(ct);
            return current;
        });
    }

    public async Task<IReadOnlyList<SmsSepAdminOption>> ListAssignableAdminsAsync(string? bearer,CancellationToken ct=default)
    {
        var root=await RequireRootAsync(bearer,ct);
        var options=await db.IdentityUsers.AsNoTracking().Where(u=>u.IsActive&&
            ((u.Id==root&&u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1)) ||
             (u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2)&&
              db.IdentityMfaStates.Any(m=>m.UserId==u.Id&&m.EnabledAtUtc!=null)&&
              db.CustomerCommercialOwners.Any(o=>o.RootLevel1UserId==root&&o.PartnerLevel2UserId==u.Id))))
            .OrderBy(u=>u.Id).Select(u=>new {u.Id,u.NormalizedEmail,u.NormalizedMobile}).ToListAsync(ct);
        return options.Select(x=>new SmsSepAdminOption(x.Id,x.NormalizedEmail??x.NormalizedMobile??x.Id.ToString())).ToArray();
    }

    internal Task<SmsSepSettings?> ReadForRootAsync(Guid root,CancellationToken ct=default)
        => db.Database.SqlQueryRaw<SmsSepSettingsRow>("""
            SELECT terminal_number AS "TerminalNumber",assigned_admin_user_id AS "AssignedAdminUserId",
              revision AS "Revision",updated_at_utc AS "UpdatedAtUtc"
            FROM sms_sep_terminal_settings WHERE root_level1_user_id={0}
            """,root).Select(x=>new SmsSepSettings(x.TerminalNumber,x.AssignedAdminUserId,x.Revision,x.UpdatedAtUtc)).SingleOrDefaultAsync(ct);

    private async Task<Guid> RequireRootAsync(string? bearer,CancellationToken ct)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)??throw new UnauthorizedAccessException();
        var allowed=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor&&u.IsActive&&
            u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1)&&db.IdentityMfaStates.Any(m=>m.UserId==actor&&m.EnabledAtUtc!=null),ct);
        if(!allowed)throw new UnauthorizedAccessException("Level 1 MFA required.");
        return actor;
    }
    private sealed class SmsSepSettingsRow { public int? TerminalNumber{get;set;} public Guid? AssignedAdminUserId{get;set;} public long Revision{get;set;} public DateTime? UpdatedAtUtc{get;set;} }
}
