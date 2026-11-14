using System.Text.Json;
using System.Text.Json.Nodes;
using ArginTaxPayer.Infrastructure.Persistence.Invoices;
using Xunit;
namespace ArginTaxPayer.Tests;

public sealed class Type2LegalDraftProfileTests
{
    [Theory]
    [InlineData("Production",1)] [InlineData("Production",2)]
    [InlineData("Sandbox",1)] [InlineData("Sandbox",2)]
    public void General_sale_profiles_are_explicitly_bound_to_the_saved_environment(string environment,int type)
    {
        var root=JsonNode.Parse(JsonSerializer.Serialize(new {environment,invoiceType=type,pattern=1}))!.AsObject();
        Assert.True(InvoiceLegalDraftProfile.Supports(root,environment));
        Assert.False(InvoiceLegalDraftProfile.Supports(root,environment=="Production"?"Sandbox":"Production"));
        root["pattern"]=4; Assert.False(InvoiceLegalDraftProfile.Supports(root,environment));
        root["pattern"]=1;root["invoiceType"]=3;Assert.False(InvoiceLegalDraftProfile.Supports(root,environment));
    }
    [Fact]
    public void Type2_draft_roundtrips_null_settlement_without_inventing_payment_fields()
    {
        var root=JsonNode.Parse("{\"invoiceType\":2,\"pattern\":1,\"settlementMethod\":0,\"cashAmountRial\":null,\"creditAmountRial\":null}")!.AsObject();
        Assert.True(InvoiceLegalDraftProfile.TrySettlement(root,0,0,0,110,out var settlement));
        settlement.Apply(root);
        Assert.Equal(0,(int?)root["settlementMethod"]);
        Assert.Null(root["cashAmountRial"]);Assert.Null(root["creditAmountRial"]);
        using var doc=JsonDocument.Parse(root.ToJsonString());
        Assert.Equal(new LegalDraftSettlement(0,0,0),InvoiceLegalDraftProfile.ReadSettlement(doc.RootElement));
        Assert.True(InvoiceLegalDraftProfile.SameSettlement(doc.RootElement,root));
    }
    [Theory]
    [InlineData(1,110,0)] [InlineData(2,0,110)] [InlineData(0,1,0)] [InlineData(0,0,1)]
    public void Type2_rejects_client_supplied_settlement(int method,decimal cash,decimal credit)
    {
        var root=JsonNode.Parse("{\"invoiceType\":2,\"pattern\":1}")!.AsObject();
        Assert.False(InvoiceLegalDraftProfile.TrySettlement(root,method,cash,credit,110,out _));
    }
    [Theory]
    [InlineData(1,0,0,110,0)] [InlineData(2,0,0,0,110)] [InlineData(3,60,50,60,50)]
    public void Type1_keeps_existing_settlement_rules(int method,decimal cash,decimal credit,decimal expectedCash,decimal expectedCredit)
    {
        var root=JsonNode.Parse("{\"invoiceType\":1,\"pattern\":1}")!.AsObject();
        Assert.True(InvoiceLegalDraftProfile.TrySettlement(root,method,cash,credit,110,out var settlement));
        Assert.Equal(new LegalDraftSettlement(method,expectedCash,expectedCredit),settlement);
        Assert.False(InvoiceLegalDraftProfile.TrySettlement(root,0,0,0,110,out _));
    }
    [Theory]
    [InlineData(typeof(Phase18ProductionCorrectionService))]
    [InlineData(typeof(Phase18ProductionSalesReturnService))]
    [InlineData(typeof(Phase10SandboxCorrectionService))]
    [InlineData(typeof(Phase10SandboxSalesReturnService))]
    public void Each_legal_draft_view_returns_Type2_and_numeric_empty_settlement(Type service)
    {
        var json="{\"invoiceType\":2,\"pattern\":1,\"lines\":[],\"settlementMethod\":0,\"cashAmountRial\":null,\"creditAmountRial\":null}";
        var method=service.GetMethod("CreateDraftView",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!;
        var view=method.Invoke(null,[Guid.NewGuid(),Guid.NewGuid(),1L,json])!;
        using var document=JsonDocument.Parse(JsonSerializer.Serialize(view,view.GetType()));
        var root=document.RootElement;
        Assert.Equal(2,root.GetProperty("InvoiceType").GetInt32());
        Assert.Equal(0,root.GetProperty("SettlementMethod").GetInt32());
        Assert.Equal(0,root.GetProperty("CashAmountRial").GetDecimal());
        Assert.Equal(0,root.GetProperty("CreditAmountRial").GetDecimal());
    }

}
