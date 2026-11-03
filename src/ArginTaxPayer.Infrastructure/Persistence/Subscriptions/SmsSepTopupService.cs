using System.Net.Http.Json;
using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public enum SmsTopupStatus { Started=1,TokenIssued=2,CallbackReceived=3,Verified=4,Failed=5,Unknown=6 }
public sealed record SmsTopupStart(Guid TopupId,string ResNum,string Token,string PaymentUrl,string ReturnUrl);
public sealed record SmsTopupView(Guid Id,long AmountIrr,string State,DateTime CreatedAtUtc,DateTime? VerifiedAtUtc);
public sealed record SepCallback(string? ResNum,string? RefNum,string? State,string? TraceNo,string? TerminalNumber,string? Amount);
public enum SepCallbackOutcome { Success,Failed,Pending,NotFound }

/// <summary>SEP OnlinePG token initiation and mandatory server-to-server verification for SMS credit.</summary>
public sealed class SmsSepTopupService(ArginTaxPayerDbContext db,IdentityAuthorizationService identity,
    IHttpClientFactory clients,IConfiguration configuration)
{
    private const string TokenEndpoint="https://sep.shaparak.ir/onlinepg/OnlinePG";
    private const string PaymentEndpoint="https://sep.shaparak.ir/OnlinePG/OnlinePG";
    private const string VerifyEndpoint="https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/VerifyTransaction";
    private const string ReverseEndpoint="https://sep.shaparak.ir/verifyTxnRandomSessionkey/ipg/ReverseTransaction";
    private const long MinimumAmountIrr=10_000,MaximumAmountIrr=100_000_000_000;

    public async Task<SmsTopupStart> StartAsync(string? bearer,long amountIrr,Guid? targetCustomerId=null,CancellationToken ct=default)
    {
        if(amountIrr is < MinimumAmountIrr or > MaximumAmountIrr)throw new ArgumentOutOfRangeException(nameof(amountIrr));
        if(configuration.GetValue<bool>("SmsSep:EnablePayments")!=true)throw new InvalidOperationException("SEP SMS payments are disabled.");
        var callback=HttpsUrl(configuration["SmsSep:CallbackUrl"]);
        var returnUrl=HttpsUrl(configuration["SmsSep:ReturnUrl"]);
        var actor=await identity.CurrentUserIdAsync(bearer,ct)??throw new UnauthorizedAccessException();
        var caller=await db.IdentityUsers.AsNoTracking().Where(u=>u.Id==actor&&u.IsActive).Select(u=>new {
            Customer=u.Roles.Any(r=>r.Role==IdentityRoleNames.Customer),
            L1=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1),L2=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2),
            Mfa=db.IdentityMfaStates.Any(m=>m.UserId==u.Id&&m.EnabledAtUtc!=null)}).SingleOrDefaultAsync(ct);
        if(caller is null)throw new UnauthorizedAccessException();
        Guid customerId,root;Guid? partner;
        if(caller.Customer)
        {
            if(targetCustomerId is not null)throw new UnauthorizedAccessException();
            customerId=actor;
            var owner=await db.CustomerCommercialOwners.AsNoTracking().SingleOrDefaultAsync(x=>x.CustomerUserId==customerId,ct)
                ??throw new InvalidOperationException("Customer has no commercial owner.");
            root=owner.RootLevel1UserId;partner=owner.PartnerLevel2UserId;
        }
        else if((caller.L1||caller.L2)&&caller.Mfa&&targetCustomerId is { } target)
        {
            customerId=target;
            var owner=await db.CustomerCommercialOwners.AsNoTracking().SingleOrDefaultAsync(x=>x.CustomerUserId==customerId,ct)
                ??throw new ArgumentException("Customer has no commercial owner.");
            if(caller.L1&&owner.RootLevel1UserId!=actor||!caller.L1&&(!caller.L2||owner.PartnerLevel2UserId!=actor))
                throw new UnauthorizedAccessException();
            root=owner.RootLevel1UserId;partner=owner.PartnerLevel2UserId;
        }
        else throw new UnauthorizedAccessException();

        var terminal=await db.Database.SqlQueryRaw<SmsSepTerminalRow>("""
            SELECT terminal_number AS "TerminalNumber",assigned_admin_user_id AS "AssignedAdminUserId"
            FROM sms_sep_terminal_settings WHERE root_level1_user_id={0}
            """,root).SingleOrDefaultAsync(ct)??throw new InvalidOperationException("SEP terminal is not configured.");
        if(!caller.Customer&&caller.L2&&!caller.L1&&terminal.AssignedAdminUserId!=actor)
            throw new UnauthorizedAccessException("SEP terminal is assigned to another administrator.");
        var mobile=await db.IdentityUsers.AsNoTracking().Where(u=>u.Id==customerId).Select(u=>u.NormalizedMobile).SingleOrDefaultAsync(ct);
        var cell=mobile is not null&&IranianMobileNumber.TryNormalize(mobile,out var normalized)?normalized[4..]:null;
        var topupId=Guid.NewGuid();var resNum=Guid.NewGuid().ToString("N");var now=DateTime.UtcNow;
        await using(var tx=await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_credit_accounts(customer_user_id,granted_irr,spent_irr,reserved_irr,revision,updated_at_utc)
                VALUES({customerId},0,0,0,1,{now}) ON CONFLICT(customer_user_id) DO NOTHING
                """,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_credit_topups(id,customer_user_id,root_level1_user_id,partner_level2_user_id,initiated_by_user_id,assigned_admin_user_id,terminal_number,amount_irr,res_num,state,created_at_utc,updated_at_utc)
                VALUES({topupId},{customerId},{root},{partner},{actor},{terminal.AssignedAdminUserId},{terminal.TerminalNumber},{amountIrr},{resNum},1,{now},{now})
                """,ct);
            await AddEventAsync(topupId,"initiated",null,null,null,null,null,ct);
            await tx.CommitAsync(ct);
        }

        try
        {
            var payload=new {Action="Token",TerminalId=terminal.TerminalNumber,Amount=amountIrr,ResNum=resNum,RedirectUrl=callback,CellNumber=cell};
            using var response=await clients.CreateClient("sep-sms").PostAsJsonAsync(TokenEndpoint,payload,ct);
            var json=await response.Content.ReadAsStringAsync(ct);
            if(!response.IsSuccessStatusCode) { await SetStateAsync(topupId,SmsTopupStatus.Failed,ct);throw new InvalidOperationException("SEP did not issue a payment token."); }
            using var doc=JsonDocument.Parse(json);
            var status=ReadLong(doc.RootElement,"status","Status");var token=ReadText(doc.RootElement,"token","Token");
            if(status!=1||string.IsNullOrWhiteSpace(token)||token.Length>512)
            {await SetStateAsync(topupId,SmsTopupStatus.Failed,ct);throw new InvalidOperationException("SEP did not issue a payment token.");}
            await SetStateAsync(topupId,SmsTopupStatus.TokenIssued,ct);
            await AddEventAsync(topupId,"token-issued",null,null,null,null,null,ct);
            return new(topupId,resNum,token,PaymentEndpoint,returnUrl);
        }
        catch(HttpRequestException)
        {await SetStateAsync(topupId,SmsTopupStatus.Unknown,ct);await AddEventAsync(topupId,"token-request-unknown",null,null,null,null,null,ct);throw new InvalidOperationException("SEP payment initiation could not be confirmed.");}
        catch(TaskCanceledException) when(!ct.IsCancellationRequested)
        {await SetStateAsync(topupId,SmsTopupStatus.Unknown,ct);await AddEventAsync(topupId,"token-request-timeout",null,null,null,null,null,ct);throw new InvalidOperationException("SEP payment initiation could not be confirmed.");}
        catch(JsonException)
        {await SetStateAsync(topupId,SmsTopupStatus.Failed,ct);await AddEventAsync(topupId,"invalid-token-response",null,null,null,null,null,ct);throw new InvalidOperationException("SEP returned an invalid token response.");}
    }

    public async Task<SepCallbackOutcome> ProcessCallbackAsync(SepCallback callback,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(callback.ResNum)||callback.ResNum.Length>64)return SepCallbackOutcome.NotFound;
        var attempt=await db.Database.SqlQueryRaw<SmsTopupAttempt>("""
            SELECT id AS "Id",customer_user_id AS "CustomerId",root_level1_user_id AS "RootId",partner_level2_user_id AS "PartnerLevel2UserId",amount_irr AS "AmountIrr",
              terminal_number AS "TerminalNumber",res_num AS "ResNum",provider_ref_num AS "ProviderRefNum",state AS "State"
            FROM sms_credit_topups WHERE res_num={0}
            """,callback.ResNum).SingleOrDefaultAsync(ct);
        if(attempt is null)return SepCallbackOutcome.NotFound;
        if(attempt.State==(short)SmsTopupStatus.Verified)
            return callback.RefNum==attempt.ProviderRefNum?SepCallbackOutcome.Success:SepCallbackOutcome.Failed;
        if(attempt.State==(short)SmsTopupStatus.Failed)return SepCallbackOutcome.Failed;
        if(attempt.State==(short)SmsTopupStatus.CallbackReceived)return SepCallbackOutcome.Pending;
        if(attempt.State!=(short)SmsTopupStatus.TokenIssued&&attempt.State!=(short)SmsTopupStatus.Unknown)return SepCallbackOutcome.Pending;
        var callbackTerminal=ParseInt(callback.TerminalNumber);
        if(string.IsNullOrWhiteSpace(callback.RefNum)||callback.RefNum.Length>128)
        {
            await AddEventAsync(attempt.Id,"callback-missing-refnum",Bound(callback.State,40),Bound(callback.RefNum,128),Bound(callback.TraceNo,80),null,callbackTerminal,ct);
            return SepCallbackOutcome.Pending;
        }
        var now=DateTime.UtcNow;
        await using(var tx=await db.Database.BeginTransactionAsync(ct))
        {
            var claimed=await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE sms_credit_topups SET state=3,callback_state={Bound(callback.State,40)},provider_trace_no={Bound(callback.TraceNo,80)},updated_at_utc={now}
                WHERE id={attempt.Id} AND state IN (2,6)
                """,ct);
            if(claimed!=1)return SepCallbackOutcome.Pending;
            await AddEventAsync(attempt.Id,"callback-received",Bound(callback.State,40),Bound(callback.RefNum,128),Bound(callback.TraceNo,80),null,callbackTerminal,ct);
            await tx.CommitAsync(ct);
        }
        Verification verification;
        try{verification=await VerifyAsync(attempt.TerminalNumber,callback.RefNum,ct);}
        catch(HttpRequestException){await SetStateAsync(attempt.Id,SmsTopupStatus.Unknown,ct);await AddEventAsync(attempt.Id,"verify-unknown",null,callback.RefNum,callback.TraceNo,null,null,ct);return SepCallbackOutcome.Pending;}
        catch(TaskCanceledException) when(!ct.IsCancellationRequested){await SetStateAsync(attempt.Id,SmsTopupStatus.Unknown,ct);await AddEventAsync(attempt.Id,"verify-timeout",null,callback.RefNum,callback.TraceNo,null,null,ct);return SepCallbackOutcome.Pending;}
        catch(OperationCanceledException){await SetStateAsync(attempt.Id,SmsTopupStatus.Unknown,CancellationToken.None);await AddEventAsync(attempt.Id,"verify-cancelled",null,callback.RefNum,callback.TraceNo,null,null,CancellationToken.None);return SepCallbackOutcome.Pending;}
        catch(JsonException){await SetStateAsync(attempt.Id,SmsTopupStatus.Unknown,ct);await AddEventAsync(attempt.Id,"verify-invalid-response",null,callback.RefNum,callback.TraceNo,null,null,ct);return SepCallbackOutcome.Pending;}
        var identityMatches=verification.RefNum==callback.RefNum&&verification.TerminalNumber==attempt.TerminalNumber;
        var amountMatches=verification.OriginalAmount==attempt.AmountIrr&&verification.AffectiveAmount==attempt.AmountIrr;
        if(!verification.Success)
        {await SetStateAsync(attempt.Id,SmsTopupStatus.Unknown,ct);await AddEventAsync(attempt.Id,"verify-not-success",null,callback.RefNum,callback.TraceNo,verification.ResultCode,verification.TerminalNumber,ct,verification.OriginalAmount,verification.Description);return SepCallbackOutcome.Pending;}
        if(!identityMatches)
        {await SetStateAsync(attempt.Id,SmsTopupStatus.Unknown,ct);await AddEventAsync(attempt.Id,"verify-identity-mismatch",null,callback.RefNum,callback.TraceNo,verification.ResultCode,verification.TerminalNumber,ct,verification.OriginalAmount,verification.Description);return SepCallbackOutcome.Pending;}
        if(!amountMatches)
        {
            var reverseResult=await TryReverseAsync(attempt.TerminalNumber,callback.RefNum,ct);
            await FailAsync(attempt.Id,verification.Description,callback.RefNum,callback.TraceNo,verification.ResultCode,ct,
                verification.OriginalAmount,verification.TerminalNumber,reverseResult);
            return SepCallbackOutcome.Failed;
        }
        try
        {
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            var finalized=await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE sms_credit_topups SET state=4,provider_ref_num={callback.RefNum},provider_trace_no={Bound(callback.TraceNo,80)},
                  verified_at_utc={DateTime.UtcNow},credited_at_utc={DateTime.UtcNow},updated_at_utc={DateTime.UtcNow}
                WHERE id={attempt.Id} AND state=3
                """,ct);
            if(finalized!=1){await tx.CommitAsync(ct);return SepCallbackOutcome.Pending;}
            var operation="sep-sms-topup:"+attempt.Id.ToString("N");
            var inserted=await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_credit_events(id,customer_user_id,operation_key,event_kind,granted_delta_irr,spent_delta_irr,reserved_delta_irr,dispatch_id,reference,recorded_at_utc)
                VALUES({Guid.NewGuid()},{attempt.CustomerId},{operation},3,{attempt.AmountIrr},0,0,NULL,{"sep-resnum:"+attempt.ResNum},{DateTime.UtcNow})
                ON CONFLICT(customer_user_id,operation_key) DO NOTHING
                """,ct);
            if(inserted==1)
            {
                var updated=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sms_credit_accounts SET granted_irr=granted_irr+{attempt.AmountIrr},revision=revision+1,updated_at_utc={DateTime.UtcNow} WHERE customer_user_id={attempt.CustomerId}",ct);
                if(updated!=1)throw new InvalidOperationException("Customer SMS credit account disappeared during verified top-up.");
            }
            await AddEventAsync(attempt.Id,inserted==1?"verified-and-credited":"duplicate-credit-already-posted",callback.State,callback.RefNum,callback.TraceNo,verification.ResultCode,verification.TerminalNumber,ct,verification.OriginalAmount,verification.Description);
            await tx.CommitAsync(ct);
            return SepCallbackOutcome.Success;
        }
        catch(PostgresException ex) when(ex.SqlState==PostgresErrorCodes.UniqueViolation)
        {await FailAsync(attempt.Id,"duplicate-provider-reference",callback.RefNum,callback.TraceNo,verification.ResultCode,ct);return SepCallbackOutcome.Failed;}
    }

    public async Task<SmsTopupView> ReadAsync(string? bearer,Guid topupId,CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)??throw new UnauthorizedAccessException();
        var isCustomer=await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==actor&&u.IsActive&&u.Roles.Any(r=>r.Role==IdentityRoleNames.Customer),ct);
        var allowed=await db.Database.SqlQueryRaw<int>("""
            SELECT count(*)::integer AS "Value" FROM sms_credit_topups t JOIN identity_users u ON u.id={0}
            WHERE t.id={1} AND (t.customer_user_id={0} OR
              (u.is_active AND (EXISTS(SELECT 1 FROM identity_user_roles r WHERE r.user_id={0} AND r.role='Level1' AND t.root_level1_user_id={0} AND EXISTS(SELECT 1 FROM identity_mfa_states m WHERE m.user_id={0} AND m.enabled_at_utc IS NOT NULL)) OR
               EXISTS(SELECT 1 FROM identity_user_roles r WHERE r.user_id={0} AND r.role='Level2' AND t.partner_level2_user_id={0} AND EXISTS(SELECT 1 FROM identity_mfa_states m WHERE m.user_id={0} AND m.enabled_at_utc IS NOT NULL)))))
            """,actor,topupId).SingleAsync(ct);
        if(allowed!=1||!isCustomer&& !await IsAdminAsync(actor,ct))throw new UnauthorizedAccessException();
        var row=await db.Database.SqlQueryRaw<SmsTopupViewRow>("""
            SELECT id AS "Id",amount_irr AS "AmountIrr",state AS "State",created_at_utc AS "CreatedAtUtc",verified_at_utc AS "VerifiedAtUtc"
            FROM sms_credit_topups WHERE id={0}
            """,topupId).SingleAsync(ct);
        return new(row.Id,row.AmountIrr,StateName(row.State),row.CreatedAtUtc,row.VerifiedAtUtc);
    }

    public async Task<SepCallbackOutcome> ReconcileAsync(string? bearer,Guid topupId,CancellationToken ct=default)
    {
        var actor=await identity.CurrentUserIdAsync(bearer,ct)??throw new UnauthorizedAccessException();
        var admin=await db.IdentityUsers.AsNoTracking().Where(u=>u.Id==actor&&u.IsActive).Select(u=>new{
            L1=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1),L2=u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2),
            Mfa=db.IdentityMfaStates.Any(m=>m.UserId==actor&&m.EnabledAtUtc!=null)}).SingleOrDefaultAsync(ct);
        if(admin is null||!admin.Mfa||!admin.L1&&!admin.L2)throw new UnauthorizedAccessException();
        var attempt=await db.Database.SqlQueryRaw<SmsTopupAttempt>("""
            SELECT id AS "Id",customer_user_id AS "CustomerId",root_level1_user_id AS "RootId",partner_level2_user_id AS "PartnerLevel2UserId",amount_irr AS "AmountIrr",
              terminal_number AS "TerminalNumber",res_num AS "ResNum",provider_ref_num AS "ProviderRefNum",state AS "State"
            FROM sms_credit_topups WHERE id={0}
            """,topupId).SingleOrDefaultAsync(ct)??throw new ArgumentException("Top-up was not found.");
        if(admin.L1&&attempt.RootId!=actor||!admin.L1&&attempt.PartnerLevel2UserId!=actor)throw new UnauthorizedAccessException();
        if(attempt.State==(short)SmsTopupStatus.Verified)return SepCallbackOutcome.Success;
        if(attempt.State!=(short)SmsTopupStatus.Unknown)return SepCallbackOutcome.Pending;
        var evidence=await db.Database.SqlQueryRaw<TopupRefEvidence>("""
            SELECT provider_ref_num AS "RefNum",provider_trace_no AS "TraceNo"
            FROM sms_credit_topup_events WHERE topup_id={0} AND provider_ref_num IS NOT NULL
            ORDER BY recorded_at_utc DESC,id DESC LIMIT 1
            """,topupId).SingleOrDefaultAsync(ct);
        if(evidence?.RefNum is null)return SepCallbackOutcome.Pending;
        return await ProcessCallbackAsync(new SepCallback(attempt.ResNum,evidence.RefNum,"OK",evidence.TraceNo,
            attempt.TerminalNumber.ToString(),null),ct);
    }

    public string BuildReturnUrl(Guid? topupId,SepCallbackOutcome outcome)
    {
        var builder=new UriBuilder(HttpsUrl(configuration["SmsSep:ReturnUrl"]));
        var addition="smsTopupStatus="+Uri.EscapeDataString(outcome.ToString().ToLowerInvariant());
        if(topupId is { } id)addition+="&smsTopupId="+Uri.EscapeDataString(id.ToString("D"));
        var existing=builder.Query.TrimStart('?');builder.Query=string.IsNullOrEmpty(existing)?addition:existing+"&"+addition;
        return builder.Uri.ToString();
    }

    public async Task<Guid?> FindTopupIdForCallbackAsync(string? resNum,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(resNum))return null;
        var row=await db.Database.SqlQueryRaw<TopupIdRow>("SELECT id AS \"Id\" FROM sms_credit_topups WHERE res_num={0}",resNum).SingleOrDefaultAsync(ct);
        return row?.Id;
    }

    public async Task ReleaseCallbackToUnknownAsync(string? resNum,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(resNum))return;
        var row=await db.Database.SqlQueryRaw<TopupIdRow>("SELECT id AS \"Id\" FROM sms_credit_topups WHERE res_num={0} AND state=3",resNum).SingleOrDefaultAsync(ct);
        if(row is null)return;
        var changed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sms_credit_topups SET state=6,updated_at_utc={DateTime.UtcNow} WHERE id={row.Id} AND state=3",ct);
        if(changed==1)await AddEventAsync(row.Id,"callback-handler-unknown",null,null,null,null,null,ct);
    }

    private async Task<Verification> VerifyAsync(int terminal,string refNum,CancellationToken ct)
    {
        var response=await clients.CreateClient("sep-sms").PostAsJsonAsync(VerifyEndpoint,new{TerminalNumber=terminal,RefNum=refNum},ct);
        var body=await response.Content.ReadAsStringAsync(ct);if(!response.IsSuccessStatusCode)throw new HttpRequestException("SEP verification HTTP failure.");
        using var doc=JsonDocument.Parse(body);var root=doc.RootElement;
        var detail=Find(root,"TransactionDetail","transactionDetail");
        return new(ReadBool(root,"Success","success")&&ReadInt(root,"ResultCode","resultCode")==0,
            ReadInt(root,"ResultCode","resultCode"),ReadText(root,"ResultDescription","resultDescription"),
            ReadText(detail,"RefNum","refNum"),ReadInt(detail,"TerminalNumber","terminalNumber"),
            ReadLong(detail,"OrginalAmount","OriginalAmount","orginalAmount","originalAmount"),
            ReadLong(detail,"AffectiveAmount","affectiveAmount"));
    }

    private async Task<int?> TryReverseAsync(int terminal,string refNum,CancellationToken ct)
    {
        try
        {
            using var response=await clients.CreateClient("sep-sms").PostAsJsonAsync(ReverseEndpoint,new{TerminalNumber=terminal,RefNum=refNum},ct);
            using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return response.IsSuccessStatusCode?ReadInt(doc.RootElement,"ResultCode","resultCode"):null;
        }
        catch(HttpRequestException){return null;}
        catch(TaskCanceledException) when(!ct.IsCancellationRequested){return null;}
        catch(JsonException){return null;}
    }

    private async Task FailAsync(Guid id,string? description,string? refNum,string? trace,int? resultCode,CancellationToken ct,long? amount=null,int? terminal=null,int? reverseResult=null)
    {
        await SetStateAsync(id,SmsTopupStatus.Failed,ct);
        await AddEventAsync(id,reverseResult is null?"verification-failed":"verification-failed-reverse-requested",null,Bound(refNum,128),Bound(trace,80),resultCode,terminal,ct,amount,Bound(description,240),reverseResult);
    }
    private async Task SetStateAsync(Guid id,SmsTopupStatus state,CancellationToken ct)
    { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE sms_credit_topups SET state={(short)state},updated_at_utc={DateTime.UtcNow} WHERE id={id} AND state NOT IN (4,5)",ct); }
    private async Task AddEventAsync(Guid id,string kind,string? callbackState,string? refNum,string? trace,int? resultCode,int? terminal,CancellationToken ct,long? amount=null,string? description=null,int? reverseResultCode=null)
    { await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sms_credit_topup_events(id,topup_id,event_kind,callback_state,provider_ref_num,provider_trace_no,verify_result_code,verify_description,reverse_result_code,verified_amount_irr,verified_terminal_number,recorded_at_utc)
            VALUES({Guid.NewGuid()},{id},{kind},{Bound(callbackState,40)},{Bound(refNum,128)},{Bound(trace,80)},{resultCode},{Bound(description,240)},{reverseResultCode},{amount},{terminal},{DateTime.UtcNow})
            """,ct); }
    private static string HttpsUrl(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps?uri.ToString():throw new InvalidOperationException("SEP callback and return URLs must be absolute HTTPS URLs.");
    private static string? Bound(string? value,int max)=>string.IsNullOrEmpty(value)?null:value.Length<=max?value:value[..max];
    private static JsonElement Find(JsonElement root,params string[] names){if(root.ValueKind!=JsonValueKind.Object)return default;foreach(var p in root.EnumerateObject())if(names.Any(n=>string.Equals(p.Name,n,StringComparison.OrdinalIgnoreCase)))return p.Value;return default;}
    private static string? ReadText(JsonElement root,params string[] names){if(root.ValueKind!=JsonValueKind.Object)return null;foreach(var p in root.EnumerateObject())if(names.Any(n=>string.Equals(p.Name,n,StringComparison.OrdinalIgnoreCase)))return p.Value.ValueKind==JsonValueKind.String?p.Value.GetString():p.Value.ToString();return null;}
    private static long? ReadLong(JsonElement root,params string[] names){var s=ReadText(root,names);return long.TryParse(s,out var v)?v:null;}
    private static int? ReadInt(JsonElement root,params string[] names){var s=ReadText(root,names);return int.TryParse(s,out var v)?v:null;}
    private static bool ReadBool(JsonElement root,params string[] names){var s=ReadText(root,names);return bool.TryParse(s,out var b)&&b;}
    private static int? ParseInt(string? value)=>int.TryParse(value,out var v)?v:null;
    private static string StateName(short state)=>state switch{1=>"started",2=>"token_issued",3=>"callback_received",4=>"verified",5=>"failed",_=>"unknown"};
    private async Task<bool> IsAdminAsync(Guid id,CancellationToken ct)=>await db.IdentityUsers.AsNoTracking().AnyAsync(u=>u.Id==id&&u.IsActive&&(u.Roles.Any(r=>r.Role==IdentityRoleNames.Level1)||u.Roles.Any(r=>r.Role==IdentityRoleNames.Level2))&&db.IdentityMfaStates.Any(m=>m.UserId==id&&m.EnabledAtUtc!=null),ct);
    private sealed class SmsSepTerminalRow{public int TerminalNumber{get;set;} public Guid AssignedAdminUserId{get;set;}}
    private sealed class SmsTopupAttempt{public Guid Id{get;set;} public Guid CustomerId{get;set;} public Guid RootId{get;set;} public Guid? PartnerLevel2UserId{get;set;} public long AmountIrr{get;set;} public int TerminalNumber{get;set;} public string ResNum{get;set;}=""; public string? ProviderRefNum{get;set;} public short State{get;set;}}
    private sealed class SmsTopupViewRow{public Guid Id{get;set;} public long AmountIrr{get;set;} public short State{get;set;} public DateTime CreatedAtUtc{get;set;} public DateTime? VerifiedAtUtc{get;set;}}
    private sealed class TopupIdRow{public Guid Id{get;set;}}
    private sealed class TopupRefEvidence{public string? RefNum{get;set;} public string? TraceNo{get;set;}}
    private sealed record Verification(bool Success,int? ResultCode,string? Description,string? RefNum,int? TerminalNumber,long? OriginalAmount,long? AffectiveAmount);
}
