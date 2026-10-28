using System.Text.Json;
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
                if(!e.TryGetProperty(name,out var v) || e.TryGetProperty(char.ToUpperInvariant(name[0])+name[1..],out v))return null;
                return v.ValueKind switch {
                    JsonValueKind.String=>v.GetString(),
                    JsonValueKind.Number=>v.GetRawText(),
                    _=>null
                };
            }
            if(!root.TryGetProperty("lines",out var source)||source.ValueKind!=JsonValueKind.Array
                ||source.GetArrayLength()>150)return (InvoiceApiDecision.Invalid,null);
            var lines=new List<object>();
            decimal gross=0,discountTotal=0,vatTotal=0;
            foreach(var row in source.EnumerateArray())
            {
                if(row.ValueKind!=JsonValueKind.Object)return (InvoiceApiDecision.Invalid,null);
                static decimal Number(JsonElement e,string key)
                {
                    if(!e.TryGetProperty(key,out var v) && !e.TryGetProperty(char.ToUpperInvariant(key[0])+key[1..],out v))return 0;
                    if(v.ValueKind==JsonValueKind.Number&&v.TryGetDecimal(out var n))return n;
                    if(v.ValueKind==JsonValueKind.String&&decimal.TryParse(v.GetString(),
                        System.Globalization.NumberStyles.Number,
                        System.Globalization.CultureInfo.InvariantCulture,out var parsed))return parsed;
                    return 0;
                }
                var quantity=Number(row,"quantity");
                var unitPrice=Number(row,"unitPriceRial");
                var discount=Number(row,"discountPerUnitRial")*quantity;
                var vatRate=Number(row,"vatRatePercent");
                var before=quantity*unitPrice;
                var net=before-discount;
                var tax=decimal.Round(net*vatRate/100,0,MidpointRounding.AwayFromZero);
                gross+=before;discountTotal+=discount;vatTotal+=tax;
                lines.Add(new {
                    description=String(row,"description")??"کالا/خدمت",
                    officialIdentifier=String(row,"officialIdentifierSnapshot"),
                    quantity,unitPriceRial=unitPrice,discountRial=discount,
                    netRial=net,vatRatePercent=vatRate,vatRial=tax,totalRial=net+tax
                });
            }
            var seller=await db.TaxpayerProfiles.AsNoTracking().Where(t=>t.Id==taxpayerId).Select(t=>t.DisplayName).FirstOrDefaultAsync(ct);
            var buyerId=String(root,"buyerPartyId");
            var buyer=Guid.TryParse(buyerId,out var partyId)
                ?await db.Parties.AsNoTracking().Where(p=>p.Id==partyId&&p.OwnerCustomerUserId==owner.Value).Select(p=>p.DisplayName).FirstOrDefaultAsync(ct)
                :null;
            return (InvoiceApiDecision.Success,new {
                invoiceId,revision=invoice.Revision,issueDateUtc=invoice.IssueDateUtc,
                seller=seller??String(root,"seller")??String(root,"sellerDisplayName"),
                buyer=buyer??String(root,"buyer")??String(root,"BuyerDisplayName"),
                description=String(root,"invoiceDescription"),
                contractNumber=String(root,"contractNumber"),
                settlementMethod=String(root,"settlementMethod"),
                lines,grossRial=gross,discountRial=discountTotal,
                netRial=gross-discountTotal,vatRial=vatTotal,
                totalRial=gross-discountTotal+vatTotal
            });
        }
        catch(JsonException){return (InvoiceApiDecision.Invalid,null);}
    }
}
