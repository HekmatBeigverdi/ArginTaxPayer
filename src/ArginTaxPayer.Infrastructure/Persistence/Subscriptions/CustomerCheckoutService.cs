using System.Data;
using System.Data.Common;
using System.Text.Json;
using ArginTaxPayer.Domain.Subscriptions;
using ArginTaxPayer.Infrastructure.Persistence.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ArginTaxPayer.Infrastructure.Persistence.Subscriptions;

/// <summary>Customer-originated, immutable, cash-pending checkout. No PSP capture is
/// represented by this service. Prices and commercial ownership are resolved server-side.</summary>
public sealed class CustomerCheckoutService(ArginTaxPayerDbContext db,
    IdentityAuthorizationService identity, SubscriptionCatalogRepository catalog)
{
    public sealed record Offer(Guid PlanId,string Code,SubscriptionPlanKind Kind,
        int ValidityDays,long? InvoiceUnitLimit,long PriceIrr);
    public sealed record CashCheckout(Guid OrderId,long BaseIrr,long VatIrr,long TotalIrr,
        bool OfficialInvoiceRequested,string PaymentStatus);

    public async Task<Offer[]> OffersAsync(string? bearer,CancellationToken ct=default)
    {
        var customer=await CurrentCustomerAsync(bearer,ct);
        var owner=await db.CustomerCommercialOwners.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.CustomerUserId==customer,ct)
            ?? throw new InvalidOperationException("Commercial owner not assigned.");
        var partner=owner.PartnerLevel2UserId;
        var now=DateTime.UtcNow;
        var rows=await db.SubscriptionPlans.AsNoTracking()
            .Where(p=>p.IsActive)
            .Join(db.SubscriptionPrices.AsNoTracking()
                .Where(p=>p.RootAdminUserId==owner.RootLevel1UserId &&
                    p.PartnerUserId==partner && p.EffectiveFromUtc<=now &&
                    (p.EffectiveUntilUtc==null || p.EffectiveUntilUtc>now)),
                p=>p.Id,q=>q.PlanId,(p,q)=>new {p,q})
            .ToListAsync(ct);
        return rows.GroupBy(x=>x.p.Id).Where(g=>g.Count()==1).Select(g=>{
            var x=g.Single();return new Offer(x.p.Id,x.p.Code,x.p.Kind,
                x.p.ValidityDays,x.p.InvoiceUnitLimit,x.q.PriceIrr);
        }).OrderBy(x=>x.PriceIrr).ToArray();
    }

    public Task<CashCheckout> RequestCashAsync(string? bearer,Guid planId,
        bool wantsOfficialInvoice,int vatBasisPoints,string operationKey,CancellationToken ct=default)
    {
        if(planId==Guid.Empty || string.IsNullOrWhiteSpace(operationKey) ||
            operationKey.Length>150 || vatBasisPoints is <0 or >10000)
            throw new ArgumentException("Invalid checkout.");
        return db.Database.CreateExecutionStrategy().ExecuteAsync(
            () => RequestCashCoreAsync(bearer,planId,wantsOfficialInvoice,vatBasisPoints,operationKey,ct));
    }

    private async Task<CashCheckout> RequestCashCoreAsync(string? bearer,Guid planId,
        bool wantsOfficialInvoice,int vatBasisPoints,string operationKey,CancellationToken ct)
    {
        var customer=await CurrentCustomerAsync(bearer,ct);
        var owner=await db.CustomerCommercialOwners.AsNoTracking()
            .SingleOrDefaultAsync(x=>x.CustomerUserId==customer,ct)
            ?? throw new InvalidOperationException("Commercial owner not assigned.");
        var partner=owner.PartnerLevel2UserId;
        // Client VAT inputs must never choose the tax rate.
        if(vatBasisPoints!=0) throw new ArgumentException("Client tax rate not accepted.");
        var taxSetting=await db.Database.SqlQueryRaw<TaxSettingRow>(
            """
            SELECT (value_json->>'basis_points')::integer AS "BasisPoints"
            FROM application_settings WHERE scope_user_id={0} AND setting_key='checkout.vat'
            """,owner.RootLevel1UserId).SingleOrDefaultAsync(ct);
        if(wantsOfficialInvoice && taxSetting is null)
            throw new InvalidOperationException("Official checkout VAT not configured.");
        var quote=await catalog.QuoteAsync(customer,planId,
            owner.RootLevel1UserId,partner,DateTime.UtcNow,ct);
        var cost=new SubscriptionCheckoutQuote(quote.PriceIrr,
            wantsOfficialInvoice?taxSetting!.BasisPoints:0,wantsOfficialInvoice);
        var now=DateTime.UtcNow;
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var conn=db.Database.GetDbConnection();
        await using(var customerLock=conn.CreateCommand())
        {
            customerLock.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
            customerLock.CommandText="SELECT id FROM identity_users WHERE id=@customer FOR UPDATE";
            Add(customerLock,"customer",customer);
            await customerLock.ExecuteScalarAsync(ct);
        }
        await using(var previous=conn.CreateCommand())
        {
            previous.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
            previous.CommandText="""
                SELECT o.id,o.plan_id,x.official_invoice_requested,x.base_amount_irr,
                       x.vat_amount_irr,x.total_amount_irr
                FROM commercial_orders o JOIN commercial_checkout_order_options x
                  ON x.order_id=o.id
                WHERE o.customer_user_id=@customer AND o.idempotency_key=@key
                FOR UPDATE OF o
                """;
            Add(previous,"customer",customer);Add(previous,"key",operationKey);
            await using var reader=await previous.ExecuteReaderAsync(ct);
            if(await reader.ReadAsync(ct))
            {
                if(reader.GetGuid(1)!=planId || reader.GetBoolean(2)!=wantsOfficialInvoice)
                    throw new InvalidOperationException("Checkout idempotency conflict.");
                var replay=new CashCheckout(reader.GetGuid(0),reader.GetInt64(3),
                    reader.GetInt64(4),reader.GetInt64(5),reader.GetBoolean(2),
                    "AwaitingCashConfirmation");
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return replay;
            }
        }
        var requestedKind=await db.SubscriptionPlans.AsNoTracking()
            .Where(x=>x.Id==planId && x.IsActive).Select(x=>x.Kind).SingleAsync(ct);
        var activeKinds=await db.CustomerSubscriptions.AsNoTracking()
            .Where(x=>x.CustomerUserId==customer && x.State==SubscriptionLifecycle.Active &&
                x.ActivatedAtUtc!=null && x.ActivatedAtUtc<=now &&
                x.ExpiresAtUtc!=null && x.ExpiresAtUtc>now)
            .Select(x=>x.Kind).Distinct().ToArrayAsync(ct);
        if(activeKinds.Any(kind=>kind!=requestedKind) ||
           (requestedKind!=SubscriptionPlanKind.Quantity && activeKinds.Length>0))
            throw new InvalidOperationException("اشتراک فعلی شما هنوز فعال است؛ درخواست پلن جدید پس از پایان آن امکان‌پذیر است.");
        await using(var pendingPlanOrder=conn.CreateCommand())
        {
            pendingPlanOrder.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
            pendingPlanOrder.CommandText="""
                SELECT EXISTS(
                  SELECT 1 FROM commercial_orders o
                  JOIN subscription_plan_catalog p ON p.id=o.plan_id
                  LEFT JOIN commercial_order_activations a ON a.order_id=o.id
                  WHERE o.customer_user_id=@customer AND o.status<>4 AND a.order_id IS NULL
                    AND (@kind<>2 OR p.kind<>2))
                """;
            Add(pendingPlanOrder,"customer",customer);Add(pendingPlanOrder,"kind",(int)requestedKind);
            if(await pendingPlanOrder.ExecuteScalarAsync(ct) is true)
                throw new InvalidOperationException("درخواست قبلی شما هنوز در انتظار بررسی است؛ تا تعیین وضعیت آن درخواست ناسازگار دیگری ثبت نکنید.");
        }
        await using var cmd=conn.CreateCommand();
        cmd.Transaction=db.Database.CurrentTransaction!.GetDbTransaction();
        cmd.CommandText="""
            INSERT INTO commercial_orders
            (id,customer_user_id,plan_id,price_version_id,root_admin_user_id,
             partner_user_id,idempotency_key,gross_amount_irr,collected_amount_irr,
             status,issued_at_utc)
            VALUES(@id,@customer,@plan,@price,@root,@partner,@key,@amount,0,1,@at)
            ON CONFLICT(customer_user_id,idempotency_key) DO NOTHING
            """;
        var id=Guid.NewGuid();
        Add(cmd,"id",id);Add(cmd,"customer",customer);Add(cmd,"plan",planId);
        Add(cmd,"price",quote.PriceVersionId);Add(cmd,"root",owner.RootLevel1UserId);
        Add(cmd,"partner",partner ?? (object)DBNull.Value);
        Add(cmd,"key",operationKey);Add(cmd,"amount",cost.TotalIrr);Add(cmd,"at",now);
        if(await cmd.ExecuteNonQueryAsync(ct)==1)
        {
            await using var invoice=conn.CreateCommand();
            invoice.Transaction=cmd.Transaction;
            invoice.CommandText="""
                INSERT INTO commercial_invoice_snapshots
                (order_id,invoice_number,invoice_json,created_at_utc)
                VALUES(@id,@number,CAST(@json AS jsonb),@at)
                """;
            Add(invoice,"id",id);Add(invoice,"number","FIN-"+id.ToString("N").ToUpperInvariant());
            Add(invoice,"json",JsonSerializer.Serialize(new {
                PlanId=planId,PriceVersionId=quote.PriceVersionId,BaseIrr=cost.BaseAmountIrr,
                VatIrr=cost.TaxAmountIrr,GrossIrr=cost.TotalIrr,
                OfficialInvoiceRequested=wantsOfficialInvoice,PaymentMethod="cash",CustomerUserId=customer
            }));Add(invoice,"at",now);
            await invoice.ExecuteNonQueryAsync(ct);
            await using var options=conn.CreateCommand();
            options.Transaction=cmd.Transaction;
            options.CommandText="""
                INSERT INTO commercial_checkout_order_options
                  (order_id,official_invoice_requested,vat_basis_points,base_amount_irr,
                   vat_amount_irr,total_amount_irr,payment_method,purchase_purpose)
                VALUES(@id,@official,@rate,@base,@vat,@total,1,1)
                """;
            Add(options,"id",id);Add(options,"official",wantsOfficialInvoice);
            Add(options,"rate",cost.VatBasisPoints);Add(options,"base",cost.BaseAmountIrr);
            Add(options,"vat",cost.TaxAmountIrr);Add(options,"total",cost.TotalIrr);
            await options.ExecuteNonQueryAsync(ct);
        }
        else
        {
            await using var previous=conn.CreateCommand();
            previous.Transaction=cmd.Transaction;
            previous.CommandText="""
                SELECT id,plan_id,price_version_id,gross_amount_irr,partner_user_id
                FROM commercial_orders WHERE customer_user_id=@customer AND idempotency_key=@key
                """;
            Add(previous,"customer",customer);Add(previous,"key",operationKey);
            await using var reader=await previous.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct))throw new InvalidOperationException("Checkout race.");
            id=reader.GetGuid(0);
            if(reader.GetGuid(1)!=planId || reader.GetGuid(2)!=quote.PriceVersionId ||
                reader.GetInt64(3)!=cost.TotalIrr ||
                (reader.IsDBNull(4)?null:reader.GetGuid(4))!=partner)
                throw new InvalidOperationException("Checkout idempotency conflict.");
        }
        await tx.CommitAsync(ct);
        return new CashCheckout(id,cost.BaseAmountIrr,cost.TaxAmountIrr,
            cost.TotalIrr,wantsOfficialInvoice,"AwaitingCashConfirmation");
    }
    private sealed class TaxSettingRow { public int BasisPoints {get;set;} }
    private async Task<Guid> CurrentCustomerAsync(string? bearer,CancellationToken ct)
    {
        var user=await identity.CurrentUserIdAsync(bearer,ct)
            ?? throw new UnauthorizedAccessException("Authentication required.");
        if(!await db.IdentityUsers.AsNoTracking().AnyAsync(x=>x.Id==user && x.IsActive,ct))
            throw new UnauthorizedAccessException("Inactive customer.");
        return user;
    }
    private static void Add(DbCommand cmd,string name,object value)
    {
        var p=cmd.CreateParameter();p.ParameterName=name;p.Value=value;cmd.Parameters.Add(p);
    }
}
