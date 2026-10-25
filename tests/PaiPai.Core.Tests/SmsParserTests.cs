using PaiPai.Core;
using Xunit;

namespace PaiPai.Core.Tests;

// These sample messages are invented placeholders. Replace them with your anonymized real messages.
public class SmsParserTests
{
    [Fact]
    public void UpiDebit()
    {
        var r = SmsParser.Parse("Rs 486.00 debited from A/c XX1234 on 25-10-26 to VPA swiggy@icici (UPI Ref 123456789012). Avl Bal Rs 12,345.67");
        Assert.NotNull(r);
        Assert.Equal(486m, r!.Amount);
        Assert.Equal(Direction.Debit, r.Direction);
        Assert.Equal("swiggy", r.Merchant);
        Assert.Equal("UPI", r.Method);
        Assert.Equal("1234", r.AccountHint);
        Assert.Equal("123456789012", r.Reference);
        Assert.Equal(12345.67m, r.Balance);
    }

    [Fact]
    public void UpiCredit()
    {
        var r = SmsParser.Parse("Rs 799.00 credited to A/c XX1234 on 25-10-26 by UPI from VPA amazon@apl. Ref 998877665544");
        Assert.NotNull(r);
        Assert.Equal(Direction.Credit, r!.Direction);
        Assert.Equal(799m, r.Amount);
        Assert.Equal("amazon", r.Merchant);
    }

    [Fact]
    public void CardSpend()
    {
        var r = SmsParser.Parse("Rs 312 spent on Card XX5678 at UBER on 25-10-26. Avl limit Rs 50,000");
        Assert.NotNull(r);
        Assert.Equal("UBER", r!.Merchant);
        Assert.Equal("Card", r.Method);
        Assert.Equal("5678", r.AccountHint);
    }

    [Theory]
    [InlineData("123456 is your OTP for transaction of Rs 500. Do not share it.")]
    [InlineData("Transaction of Rs 250 failed. Amount will be reversed.")]
    [InlineData("Get 10% cashback on your next recharge!")]
    public void NotATransaction(string body) => Assert.Null(SmsParser.Parse(body));

    [Fact]
    public void LearnedRuleBeatsBuiltIn() =>
        Assert.Equal("Shopping", Categorizer.Categorize("swiggy@icici", new Dictionary<string, string> { ["swiggy"] = "Shopping" }));
}
