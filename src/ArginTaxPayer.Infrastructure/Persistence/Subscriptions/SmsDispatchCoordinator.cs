using System.Security.Cryptography;
using System.Text;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

public enum SmsDispatchStatus { Accepted, Rejected, Ambiguous, InsufficientCredit, Unavailable }
public sealed record SmsDispatchOutcome(SmsDispatchStatus Status, Guid DispatchId, bool ProviderAccepted = false);

/// <summary>Reserves a bounded amount before provider I/O, then records provider evidence and settles exactly once.</summary>
public sealed class SmsDispatchCoordinator(ArginTaxPayerDbContext db, IIdentityOtpSender sender,
    SmsServiceSettingsService settings, CustomerCommercialOwnershipService owners,
    IConfiguration config, ILogger<SmsDispatchCoordinator> logger)
{
    public async Task<SmsDispatchOutcome> SendOtpAsync(Guid? customerId, string mobile, string code,
        string purpose, Guid dispatchId, CancellationToken ct = default)
    {
        Guid? root = null, partner = null;
        SmsServiceSettings? snapshot = null;
        var isLocal = config["Identity:OtpDeliveryMode"] == "LocalFile";
        if (!isLocal)
        {
            if (customerId is { } cid)
            {
                var owner = await db.CustomerCommercialOwners.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerUserId == cid, ct);
                root = owner?.RootLevel1UserId; partner = owner?.PartnerLevel2UserId;
            }
            else root = await owners.ResolveRootAsync(null, ct);
            if (root is null || (snapshot = await settings.ReadForRootAsync(root.Value, ct)) is null)
                return new(SmsDispatchStatus.Unavailable,dispatchId);
            if (config["Ghasedak:CostUnit"]?.Trim().ToUpperInvariant() is not ("IRR" or "TOMAN"))
                return new(SmsDispatchStatus.Unavailable,dispatchId);
        }
        var customerBillingEnabled = !isLocal && (snapshot?.CustomerBillingEnabled ?? true);
        var pepper = config["Identity:OtpPepper"];
        if (pepper is not { Length: >= 32 }) return new(SmsDispatchStatus.Unavailable,dispatchId);
        var hash = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper),Encoding.UTF8.GetBytes(mobile)));
        await using var dispatchTransaction = await db.Database.BeginTransactionAsync(ct);
        if(!isLocal)
        {
            logger.LogInformation("OTP_DISPATCH_DESTINATION_LOCK_STARTED");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({hash},0))",ct);
            logger.LogInformation("OTP_DISPATCH_DESTINATION_LOCK_ACQUIRED");
        }
        logger.LogInformation("OTP_DISPATCH_COOLDOWN_CHECK_STARTED");
        var now = DateTime.UtcNow;
        var hasRecentDispatch = !isLocal && await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (
              SELECT 1 FROM sms_provider_dispatches
              WHERE destination_hash={hash} AND (
                state IN (1,3,6) AND created_at_utc>{now.AddMinutes(-15)} OR
                state=5 AND updated_at_utc>{now.AddMinutes(-3)})
            ) AS "Value"
            """).SingleAsync(ct);
        if (hasRecentDispatch)
            return new(SmsDispatchStatus.Unavailable,dispatchId);
        logger.LogInformation("OTP_DISPATCH_COOLDOWN_CHECK_PASSED");
        long reserved = 0;
        if (!isLocal && customerBillingEnabled && customerId is { } customer)
        {
            reserved = snapshot!.ReservationCeilingIrr;
            var updated = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE sms_credit_accounts SET reserved_irr=reserved_irr+{reserved},revision=revision+1,updated_at_utc={DateTime.UtcNow}
                WHERE customer_user_id={customer} AND reserved_irr <= granted_irr-spent_irr-{reserved}
                """,ct);
            if(updated!=1) return new(SmsDispatchStatus.InsufficientCredit,dispatchId);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_provider_dispatches(id,client_reference_id,actor_user_id,sms_user_id,root_level1_user_id,partner_level2_user_id,destination_hash,purpose,state,reservation_irr,service_fee_basis_points,reservation_ceiling_snapshot_irr,customer_billing_enabled_snapshot,created_at_utc,updated_at_utc)
                VALUES({dispatchId},{dispatchId.ToString("N")},{customer},{customer},{root},{partner},{hash},{purpose},1,{reserved},{snapshot!.ServiceFeeBasisPoints},{reserved},{customerBillingEnabled},{DateTime.UtcNow},{DateTime.UtcNow})
                """,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_credit_events(id,customer_user_id,operation_key,event_kind,reserved_delta_irr,dispatch_id,reference,recorded_at_utc)
                VALUES({Guid.NewGuid()},{customer},{"sms-reserve:"+dispatchId.ToString("N")},4,{reserved},{dispatchId},'provider-dispatch-reservation',{DateTime.UtcNow})
                """,ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_provider_dispatch_events(id,dispatch_id,state,detail_code,recorded_at_utc) VALUES({Guid.NewGuid()},{dispatchId},1,'reserved',{DateTime.UtcNow})""",ct);
        }
        else if(!isLocal)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO sms_provider_dispatches(id,client_reference_id,actor_user_id,sms_user_id,root_level1_user_id,partner_level2_user_id,destination_hash,purpose,state,service_fee_basis_points,reservation_ceiling_snapshot_irr,customer_billing_enabled_snapshot,created_at_utc,updated_at_utc)
                VALUES({dispatchId},{dispatchId.ToString("N")},{customerId},{customerId},{root},{partner},{hash},{purpose},1,{snapshot!.ServiceFeeBasisPoints},{snapshot.ReservationCeilingIrr},{customerBillingEnabled},{DateTime.UtcNow},{DateTime.UtcNow})
                """,ct);
        }
        logger.LogInformation("OTP_DISPATCH_RESERVATION_COMMIT_STARTED");
        await dispatchTransaction.CommitAsync(ct);
        logger.LogInformation("OTP_DISPATCH_PROVIDER_CALL_STARTED");
    var result = await sender.SendWithCostAsync(mobile,code,purpose,dispatchId,ct);
        logger.LogInformation("OTP_DISPATCH_PROVIDER_CALL_COMPLETED accepted={Accepted} ambiguous={Ambiguous}",
            result.Accepted, result.Ambiguous);
        if (isLocal) return new(result.Accepted?SmsDispatchStatus.Accepted:SmsDispatchStatus.Rejected,dispatchId);
        long? customerCharge=null;
        if(customerBillingEnabled && customerId is not null && result.CostIrr is { } knownCost && snapshot is not null)
        { try { customerCharge=SmsCreditPolicy.CalculateCharge(knownCost,snapshot.ServiceFeeBasisPoints).TotalChargeIrr; } catch(OverflowException) { } }
        var chargeCustomer = customerBillingEnabled && customerId is not null;
        var beyondReservation = chargeCustomer && customerCharge is { } chargeCheck && chargeCheck > reserved;
        var state = result.Ambiguous ? (short)3 : !result.Accepted ? (short)4 : result.CostIrr is null || beyondReservation ? (short)6 : (short)5;
        await using (var tx=await db.Database.BeginTransactionAsync(ct))
        {
            if(result.Accepted && result.CostIrr is { } cost)
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO sms_provider_expense_events(id,dispatch_id,actor_user_id,sms_user_id,root_level1_user_id,partner_level2_user_id,purpose,provider_message_id,raw_provider_cost,raw_provider_cost_unit,provider_cost_irr,provider_sent_at_utc,recorded_at_utc)
                    VALUES({Guid.NewGuid()},{dispatchId},{customerId},{customerId},{root},{partner},{purpose},{result.MessageId},{result.RawCost},{result.CostUnit},{cost},{result.SentAtUtc},{DateTime.UtcNow}) ON CONFLICT(dispatch_id) DO NOTHING
                    """,ct);
            if(chargeCustomer && customerId is { } user && result.Accepted && result.CostIrr is { } actual && customerCharge is { } fullCharge && fullCharge<=reserved)
            {
                var fee=fullCharge-actual;
                await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE sms_credit_accounts SET spent_irr=spent_irr+{fullCharge},reserved_irr=reserved_irr-{reserved},revision=revision+1,updated_at_utc={DateTime.UtcNow} WHERE customer_user_id={user}""",ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_credit_events(id,customer_user_id,operation_key,event_kind,spent_delta_irr,reserved_delta_irr,dispatch_id,reference,recorded_at_utc) VALUES({Guid.NewGuid()},{user},{"sms-settle:"+dispatchId.ToString("N")},5,{fullCharge},{-reserved},{dispatchId},'provider-cost-settlement',{DateTime.UtcNow})""",ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_customer_sms_charges(id,dispatch_id,customer_user_id,root_level1_user_id,partner_level2_user_id,provider_cost_irr,service_fee_basis_points,service_fee_irr,customer_charge_irr,recorded_at_utc) VALUES({Guid.NewGuid()},{dispatchId},{user},{root},{partner},{actual},{snapshot.ServiceFeeBasisPoints},{fee},{fullCharge},{DateTime.UtcNow}) ON CONFLICT(dispatch_id) DO NOTHING""",ct);
            }
            else if(chargeCustomer && customerId is { } rejected && !result.Accepted && !result.Ambiguous && reserved>0)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE sms_credit_accounts SET reserved_irr=reserved_irr-{reserved},revision=revision+1,updated_at_utc={DateTime.UtcNow} WHERE customer_user_id={rejected}""",ct);
                await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_credit_events(id,customer_user_id,operation_key,event_kind,reserved_delta_irr,dispatch_id,reference,recorded_at_utc) VALUES({Guid.NewGuid()},{rejected},{"sms-release:"+dispatchId.ToString("N")},6,{-reserved},{dispatchId},'provider-rejected-release',{DateTime.UtcNow})""",ct);
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE sms_provider_dispatches SET state={state},provider_message_id={result.MessageId},updated_at_utc={DateTime.UtcNow} WHERE id={dispatchId}""",ct);
            var detail = result.Ambiguous ? "ambiguous" : !result.Accepted ? "rejected" : result.CostIrr is null || beyondReservation ? "cost-reconciliation-required" :
                purpose == "register" && customerId is null ? customerBillingEnabled ? "registration-charge-pending" : "billing-disabled-provider-cost-recorded" :
                chargeCustomer ? "settled" : customerId is null ? "administrator-provider-cost-recorded" : "billing-disabled-provider-cost-recorded";
            await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_provider_dispatch_events(id,dispatch_id,state,detail_code,recorded_at_utc) VALUES({Guid.NewGuid()},{dispatchId},{state},{detail},{DateTime.UtcNow})""",ct);
            await tx.CommitAsync(ct);
        }
        return new(result.Ambiguous?SmsDispatchStatus.Ambiguous:!result.Accepted?SmsDispatchStatus.Rejected:result.CostIrr is null||beyondReservation?SmsDispatchStatus.Ambiguous:SmsDispatchStatus.Accepted,dispatchId,result.Accepted);
    }

    public async Task LinkRegistrationDispatchWithinTransactionAsync(Guid customerId,string mobile,CancellationToken ct=default)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Registration SMS link must share account-creation transaction.");
        var pepper=config["Identity:OtpPepper"]!;var hash=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(pepper),Encoding.UTF8.GetBytes(mobile)));
        var dispatch=await db.Database.SqlQueryRaw<PendingRegistrationSms>("""
            SELECT d.id AS "Id",d.root_level1_user_id AS "RootId",d.partner_level2_user_id AS "PartnerId",d.service_fee_basis_points AS "Fee",e.provider_cost_irr AS "Cost",
              d.customer_billing_enabled_snapshot AS "CustomerBillingEnabled"
            FROM sms_provider_dispatches d LEFT JOIN sms_provider_expense_events e ON e.dispatch_id=d.id
            WHERE d.destination_hash={0} AND d.purpose='register' AND d.state IN (5,6) AND NOT EXISTS(SELECT 1 FROM sms_dispatch_customer_links l WHERE l.dispatch_id=d.id)
            ORDER BY d.created_at_utc DESC LIMIT 1 FOR UPDATE OF d
            """,hash).SingleOrDefaultAsync(ct);
        if(dispatch is null||dispatch.Fee is null||dispatch.RootId is null)return;
        var now=DateTime.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_dispatch_customer_links(dispatch_id,customer_user_id,root_level1_user_id,partner_level2_user_id,linked_at_utc) VALUES({dispatch.Id},{customerId},{dispatch.RootId},{dispatch.PartnerId},{now})""",ct);
        if(!dispatch.CustomerBillingEnabled)return;
        if(dispatch.Cost is null)return;
        SmsCustomerCharge math;try{math=SmsCreditPolicy.CalculateCharge(dispatch.Cost.Value,dispatch.Fee.Value);}catch(OverflowException){await MarkRegistrationReconciliationAsync(dispatch.Id,now,ct);return;}
        var fee=math.ServiceFeeIrr;var charge=math.TotalChargeIrr;
        var spent=await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE sms_credit_accounts SET spent_irr=spent_irr+{charge},revision=revision+1,updated_at_utc={now} WHERE customer_user_id={customerId} AND reserved_irr<=granted_irr-spent_irr-{charge}""",ct);
        if(spent!=1){await MarkRegistrationReconciliationAsync(dispatch.Id,now,ct);return;} // Preserve account creation; operator reconciles charge.
        await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_customer_sms_charges(id,dispatch_id,customer_user_id,root_level1_user_id,partner_level2_user_id,provider_cost_irr,service_fee_basis_points,service_fee_irr,customer_charge_irr,recorded_at_utc) VALUES({Guid.NewGuid()},{dispatch.Id},{customerId},{dispatch.RootId},{dispatch.PartnerId},{dispatch.Cost.Value},{dispatch.Fee.Value},{fee},{charge},{now})""",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_credit_events(id,customer_user_id,operation_key,event_kind,spent_delta_irr,dispatch_id,reference,recorded_at_utc) VALUES({Guid.NewGuid()},{customerId},{"sms-registration-charge:"+dispatch.Id.ToString("N")},5,{charge},{dispatch.Id},'registration-otp-charge',{now})""",ct);
    }
    private async Task MarkRegistrationReconciliationAsync(Guid dispatchId,DateTime now,CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE sms_provider_dispatches SET state=6,updated_at_utc={now} WHERE id={dispatchId}""",ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO sms_provider_dispatch_events(id,dispatch_id,state,detail_code,recorded_at_utc) VALUES({Guid.NewGuid()},{dispatchId},6,'registration-charge-reconciliation',{now})""",ct);
    }
    private sealed class PendingRegistrationSms { public Guid Id{get;set;} public Guid? RootId{get;set;} public Guid? PartnerId{get;set;} public int? Fee{get;set;} public long? Cost{get;set;} public bool CustomerBillingEnabled{get;set;} }
}
