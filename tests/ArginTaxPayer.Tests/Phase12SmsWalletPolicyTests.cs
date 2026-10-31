using ArginTaxPayer.Domain.Subscriptions;
using Xunit;
namespace ArginTaxPayer.Tests;
public sealed class Phase12SmsWalletPolicyTests
{
    [Fact] public void Sms_wallet_is_separate_and_conserves_units()
    {
        var initial=new SmsWalletBalance(50,0);
        var spent=initial.Spend(12);
        Assert.Equal(38,spent.Available);
        Assert.Equal(58,spent.Grant(20).Available);
    }
    [Fact] public void Cannot_overdraw_or_grant_negative_units()
    {
        Assert.Throws<InvalidOperationException>(()=>new SmsWalletBalance(5,6).Validate());
        Assert.Throws<InvalidOperationException>(()=>new SmsWalletBalance(5,1).Spend(5));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SmsWalletBalance(5,1).Grant(0));
    }
}
