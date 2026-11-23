using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace ArginTaxPayer.Infrastructure.Persistence.Invoices;

internal readonly record struct LegalDraftSettlement(int Method,decimal Cash,decimal Credit)
{
    internal void Apply(JsonObject draft)
    {
        draft["settlementMethod"]=Method;
        draft["cashAmountRial"]=Method==0 ? null : JsonValue.Create(Cash);
        draft["creditAmountRial"]=Method==0 ? null : JsonValue.Create(Credit);
    }
}

/// <summary>V7.9 general-sale legal drafts: Type 2 omits buyer and settlement data.</summary>
internal static class InvoiceLegalDraftProfile
{
    internal static bool Supports([NotNullWhen(true)] JsonObject? root,string environment) => root is not null &&
        (string?)root["environment"]==environment && (int?)root["invoiceType"] is 1 or 2 && (int?)root["pattern"]==1;

    internal static bool TrySettlement(JsonObject root,int method,decimal cash,decimal credit,
        decimal total,out LegalDraftSettlement settlement)
    {
        settlement=default;
        if ((int?)root["invoiceType"]==2 && (int?)root["pattern"]==1)
            return method==0 && cash==0 && credit==0 && total>=0;
        if ((int?)root["invoiceType"]!=1 || (int?)root["pattern"]!=1) return false;
        cash=method==1 ? total : method==2 ? 0 : cash;
        credit=method==2 ? total : method==1 ? 0 : credit;
        if (method is <1 or >3 || cash<0 || credit<0 ||
            cash!=decimal.Truncate(cash) || credit!=decimal.Truncate(credit) || cash+credit!=total ||
            method==1 && credit!=0 || method==2 && cash!=0 || method==3 && (cash<=0 || credit<=0)) return false;
        settlement=new(method,cash,credit);
        return true;
    }

    internal static LegalDraftSettlement ReadSettlement(JsonElement root) =>
        InvoiceSnapshotClassification.IsType2GeneralSale(root) ? default :
        new(root.GetProperty("settlementMethod").GetInt32(),root.GetProperty("cashAmountRial").GetDecimal(),root.GetProperty("creditAmountRial").GetDecimal());

    internal static bool SameSettlement(JsonElement source,JsonObject draft) =>
        InvoiceSnapshotClassification.IsType2GeneralSale(source) ||
        source.GetProperty("settlementMethod").GetInt32()==(int?)draft["settlementMethod"] &&
        source.GetProperty("cashAmountRial").GetDecimal()==(decimal?)draft["cashAmountRial"] &&
        source.GetProperty("creditAmountRial").GetDecimal()==(decimal?)draft["creditAmountRial"];
}
