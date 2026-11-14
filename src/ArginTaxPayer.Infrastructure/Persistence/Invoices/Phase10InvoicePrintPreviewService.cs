using System.Text.Json;
using ArginTaxPayer.Infrastructure.Persistence.Parties;
using ArginTaxPayer.Infrastructure.Persistence.Taxpayers;
using Microsoft.EntityFrameworkCore;

namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

/// <summary>Read-only, tenant-scoped official invoice preview from immutable snapshot.</summary>
public sealed class Phase10InvoicePrintPreviewService(
    ArginTaxPayerDbContext db, TaxpayerScopeService scope)
{
    public async Task<(InvoiceApiDecision Decision, object? Preview)> ReadAsync(
        string? bearer, Guid taxpayerId, Guid invoiceId, CancellationToken ct)
    {
        var actor=await scope.ResolveAsync(bearer,ct);
        if(actor.Decision==TaxpayerScopeDecision.Unauthenticated)
            return (InvoiceApiDecision.Unauthenticated,null);
        if(actor.Decision!=TaxpayerScopeDecision.Allowed)
            return (InvoiceApiDecision.Forbidden,null);
        var owner=await scope.ScopedQuery(actor.ActorId,actor.Level1)
            .Where(x=>x.Id==taxpayerId).Select(x=>x.CustomerUserId)
            .FirstOrDefaultAsync(ct);
        if(owner is null)return (InvoiceApiDecision.NotFound,null);
        var invoice=await db.InvoiceDocuments.AsNoTracking()
            .Where(x=>x.Id==invoiceId&&x.TaxpayerId==taxpayerId&&x.CustomerUserId==owner.Value)
            .Select(x=>new{x.SnapshotJson,x.IssueDateUtc,x.Revision,x.State})
            .SingleOrDefaultAsync(ct);
        if(invoice is null)return (InvoiceApiDecision.NotFound,null);
        try
        {
            using var doc=JsonDocument.Parse(invoice.SnapshotJson,new JsonDocumentOptions{MaxDepth=32});
            var root=doc.RootElement;
            static string? String(JsonElement e,string name)
            {
                if(!e.TryGetProperty(name,out var v) && !e.TryGetProperty(char.ToUpperInvariant(name[0])+name[1..],out v))return null;
                return v.ValueKind switch {
                    JsonValueKind.String=>v.GetString(),
                    JsonValueKind.Number=>v.GetRawText(),
                    _=>null
                };
            }
            static decimal Number(JsonElement e,string name,decimal fallback=0m)
            {
                if(!e.TryGetProperty(name,out var v)&&
                    !e.TryGetProperty(char.ToUpperInvariant(name[0])+name[1..],out v))return fallback;
                if(v.ValueKind==JsonValueKind.Number&&v.TryGetDecimal(out var n))return n;
                if(v.ValueKind==JsonValueKind.String&&decimal.TryParse(v.GetString(),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,out var parsed))return parsed;
                return fallback;
            }
            if(!root.TryGetProperty("lines",out var source)||source.ValueKind!=JsonValueKind.Array
                ||source.GetArrayLength()>150)return (InvoiceApiDecision.Invalid,null);
            var lines=new List<object>();
            decimal gross=0,discountTotal=0,vatTotal=0;
            foreach(var row in source.EnumerateArray())
            {
                if(row.ValueKind!=JsonValueKind.Object)return (InvoiceApiDecision.Invalid,null);
                var quantity=Number(row,"quantity");
                var unitPrice=Number(row,"unitPriceRial");
                var discount=Number(row,"discountPerUnitRial")*quantity;
                var vatRate=Number(row,"vatRatePercent");
                var before=quantity*unitPrice;
                var lineNet=before-discount;
                var tax=decimal.Round(lineNet*vatRate/100,0,MidpointRounding.AwayFromZero);
                gross+=before;discountTotal+=discount;vatTotal+=tax;
                lines.Add(new {
                    description=String(row,"description")??"کالا/خدمت",
                    officialIdentifier=String(row,"officialIdentifierSnapshot"),
                    quantity,unitPriceRial=unitPrice,grossRial=before,discountRial=discount,
                    netRial=lineNet,vatRatePercent=vatRate,vatRial=tax,totalRial=lineNet+tax
                });
            }
            var sellerProfile=await db.TaxpayerProfiles.AsNoTracking().Where(t=>t.Id==taxpayerId&&
                t.CustomerUserId==owner.Value).Select(t=>new {t.DisplayName,t.NormalizedIdentifier}).SingleOrDefaultAsync(ct);
            var sellerMetadata=await db.InvoiceParticipantDetails.AsNoTracking().Where(d=>
                d.CustomerUserId==owner.Value&&d.TaxpayerId==taxpayerId).Select(d=>new {
                    d.EconomicCode,d.BranchCode,d.PostalCode,d.Province,d.City,d.Address,d.Phone
                }).SingleOrDefaultAsync(ct);
            var logo=await db.SoftwareUserProfiles.AsNoTracking().Where(p=>p.UserId==owner.Value)
                .Select(p=>p.InvoiceLogoPng).SingleOrDefaultAsync(ct);
            var buyerId=String(root,"buyerPartyId");
            var parsedBuyerId=Guid.TryParse(buyerId,out var partyId);
            var buyerProfile=parsedBuyerId?await (from party in db.Parties.AsNoTracking()
                where party.Id==partyId&&(party.OwnerCustomerUserId==owner.Value||
                    db.CustomerPartyLinks.Any(link=>link.PartyId==partyId&&
                        link.CustomerUserId==owner.Value))
                select new {party.DisplayName,Identity=party.NormalizedIdentifier,
                    party.RegistrationNumber,Kind=(int)party.Kind}).SingleOrDefaultAsync(ct):null;
            var buyerMetadata=parsedBuyerId?await db.InvoiceParticipantDetails.AsNoTracking().Where(d=>
                d.CustomerUserId==owner.Value&&d.PartyId==partyId).Select(d=>new {
                    d.EconomicCode,d.BranchCode,d.PostalCode,d.Province,d.City,d.Address,d.Phone
                }).SingleOrDefaultAsync(ct):null;
            var buyerAddress=parsedBuyerId?await db.PartyAddresses.AsNoTracking().Where(a=>
                a.PartyId==partyId&&a.IsActive).OrderByDescending(a=>a.IsPrimary)
                .ThenByDescending(a=>a.Revision).Select(a=>new {a.PostalCode,a.Province,a.City,
                    Address=a.StreetAddress}).FirstOrDefaultAsync(ct):null;
            var buyerPhone=parsedBuyerId?await db.PartyContacts.AsNoTracking().Where(c=>
                c.PartyId==partyId&&c.IsActive).OrderByDescending(c=>c.IsPrimary)
                .ThenBy(c=>c.Kind).Select(c=>c.Value).FirstOrDefaultAsync(ct):null;
            var seller=sellerProfile?.DisplayName;
            var buyer=buyerProfile?.DisplayName;
            var sellerDetails=new {
                name=seller,identity=sellerProfile?.NormalizedIdentifier,
                economicCode=sellerMetadata?.EconomicCode,registrationNumber=(string?)null,
                branchCode=sellerMetadata?.BranchCode,postalCode=sellerMetadata?.PostalCode,
                province=sellerMetadata?.Province,city=sellerMetadata?.City,
                address=sellerMetadata?.Address,phone=sellerMetadata?.Phone,kind=(string?)null
            };
            var buyerDetails=new {
                name=buyer,identity=buyerProfile?.Identity,economicCode=buyerMetadata?.EconomicCode,
                registrationNumber=buyerProfile?.RegistrationNumber,
                branchCode=buyerMetadata?.BranchCode,postalCode=buyerMetadata?.PostalCode??buyerAddress?.PostalCode,
                province=buyerMetadata?.Province??buyerAddress?.Province,
                city=buyerMetadata?.City??buyerAddress?.City,
                address=buyerMetadata?.Address??buyerAddress?.Address,
                phone=buyerMetadata?.Phone??buyerPhone,
                kind=buyerProfile?.Kind==1?"NaturalPerson":buyerProfile is null?null:"LegalEntity"
            };
            var fees=Number(root,"otherTaxAndLegalFeesRial");
            var summary=root.TryGetProperty("financialSummary",out var savedSummary)?savedSummary:
                root.TryGetProperty("FinancialSummary",out savedSummary)?savedSummary:default;
            if(summary.ValueKind==JsonValueKind.Object)
            {
                gross=Number(summary,"beforeDiscountRial",Number(summary,"GrossRial",gross));
                discountTotal=Number(summary,"DiscountRial",discountTotal);
                vatTotal=Number(summary,"VatRial",Number(summary,"PreviewVatRial",vatTotal));
            }
            var total=summary.ValueKind==JsonValueKind.Object
                ?Number(summary,"GrandTotalRial",Number(summary,"PreviewGrandTotalRial",gross-discountTotal+vatTotal+fees))
                :gross-discountTotal+vatTotal+fees;
            var net=summary.ValueKind==JsonValueKind.Object
                ?Number(summary,"AfterDiscountRial",gross-discountTotal):gross-discountTotal;
            decimal? cashAmount=root.TryGetProperty("cashAmountRial",out _)||root.TryGetProperty("CashAmountRial",out _)
                ?Number(root,"cashAmountRial"):null;
            decimal? creditAmount=root.TryGetProperty("creditAmountRial",out _)||root.TryGetProperty("CreditAmountRial",out _)
                ?Number(root,"creditAmountRial"):null;
            string? fiscalTaxId=null;
            string? referenceFiscalTaxId=null;
            var connection=db.Database.GetDbConnection();
            var opened=connection.State!=System.Data.ConnectionState.Open;
            if(opened)await connection.OpenAsync(ct);
            try
            {
                await using var tables=connection.CreateCommand();
                tables.CommandText="""
SELECT to_regclass('public.sandbox_invoice_fiscal_reservations') IS NOT NULL,
       to_regclass('public.authority_invoice_fiscal_reservations') IS NOT NULL
""";
                await using var tableReader=await tables.ExecuteReaderAsync(ct);
                var hasSandbox=false;var hasProduction=false;
                if(await tableReader.ReadAsync(ct))
                {hasSandbox=tableReader.GetBoolean(0);hasProduction=tableReader.GetBoolean(1);}
                await tableReader.DisposeAsync();
                var sources=new List<string>();
                if(hasSandbox)sources.Add("""
SELECT fiscal_tax_id,created_at_utc FROM sandbox_invoice_fiscal_reservations
WHERE invoice_id=@invoice AND taxpayer_id=@taxpayer AND customer_user_id=@owner
""");
                if(hasProduction)sources.Add("""
SELECT fiscal_tax_id,created_at_utc FROM authority_invoice_fiscal_reservations
WHERE invoice_id=@invoice AND taxpayer_id=@taxpayer AND customer_user_id=@owner
""");
                if(sources.Count>0)
                {
                    await using var query=connection.CreateCommand();
                    query.CommandText=$"""
SELECT fiscal_tax_id FROM ({string.Join(" UNION ALL ",sources)}) reservations
ORDER BY created_at_utc DESC LIMIT 1
""";
                    foreach(var (key,value) in new (string,object)[]{
                        ("invoice",invoiceId),("taxpayer",taxpayerId),("owner",owner.Value)})
                    {
                        var parameter=query.CreateParameter();
                        parameter.ParameterName=key;parameter.Value=value;query.Parameters.Add(parameter);
                    }
                    fiscalTaxId=await query.ExecuteScalarAsync(ct) as string;
                }
            await using(var referenceQuery=connection.CreateCommand())
            {
                referenceQuery.CommandText="""
SELECT predecessor_fiscal_tax_id FROM (
 SELECT predecessor_fiscal_tax_id,created_at_utc FROM sandbox_invoice_legal_relations
 WHERE successor_invoice_id=@invoice AND customer_user_id=@owner AND taxpayer_id=@taxpayer
 UNION ALL
 SELECT predecessor_fiscal_tax_id,created_at_utc FROM authority_invoice_legal_relations
 WHERE successor_invoice_id=@invoice AND customer_user_id=@owner AND taxpayer_id=@taxpayer
) refs ORDER BY created_at_utc DESC LIMIT 1
""";
                foreach(var (key,value) in new (string,object)[]{("invoice",invoiceId),
                    ("owner",owner.Value),("taxpayer",taxpayerId)})
                {var parameter=referenceQuery.CreateParameter();parameter.ParameterName=key;
                    parameter.Value=value;referenceQuery.Parameters.Add(parameter);}
                referenceFiscalTaxId=await referenceQuery.ExecuteScalarAsync(ct) as string;
            }
            }
            finally {if(opened)await connection.CloseAsync();}
            return (InvoiceApiDecision.Success,new {
                invoiceId,uniqueInvoiceId=fiscalTaxId,revision=invoice.Revision,issueDateUtc=invoice.IssueDateUtc,
                referenceFiscalTaxId,
                invoiceType=String(root,"invoiceType"),pattern=String(root,"pattern"),
                invoiceLogoDataUrl=logo is {Length:>0}
                    ?"data:image/png;base64,"+Convert.ToBase64String(logo):null,
                seller=seller??String(root,"seller")??String(root,"sellerDisplayName"),sellerDetails,
                buyer=buyer??String(root,"buyer")??String(root,"BuyerDisplayName"),buyerDetails,
                description=String(root,"invoiceDescription"),
                contractNumber=String(root,"contractNumber"),
                settlementMethod=InvoiceSnapshotClassification.IsType2GeneralSale(root) ? "نقدی" : String(root,"settlementMethod") switch { "1"=>"نقدی", "2"=>"نسیه", "3"=>"نقدی و نسیه", _=>"نامشخص" },
                lines,grossRial=gross,discountRial=discountTotal,netRial=net,
                vatRial=vatTotal,totalRial=total,otherTaxAndLegalFeesRial=fees,
                cashAmountRial=InvoiceSnapshotClassification.IsType2GeneralSale(root) ? total : cashAmount,creditAmountRial=InvoiceSnapshotClassification.IsType2GeneralSale(root) ? 0 : creditAmount,
                paymentDateUtc=String(root,"paymentDateUtc")
            });
        }
        catch(JsonException){return (InvoiceApiDecision.Invalid,null);}
    }
}
