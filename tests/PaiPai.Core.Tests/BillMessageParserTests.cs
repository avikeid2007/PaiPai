using PaiPai.Core;
using Xunit;

namespace PaiPai.Core.Tests;

public class BillMessageParserTests
{
    [Fact]
    public void ParsesJioBillWithoutCreatingPayment()
    {
        var body = "Your 23-Oct-2026 e-bill for Jio Number 9876543210\n" +
                   "Bill period: 23-Sep-2026 to 22-Oct-2026\nTotal Amount payable: Rs. 1,414.82\n" +
                   "Payment due date: 01-Nov-2026";

        var bill = BillMessageParser.Parse(body, new DateTime(2026, 10, 23));

        Assert.NotNull(bill);
        Assert.Equal("Jio", bill!.Name);
        Assert.Equal(1414.82m, bill.Amount);
        Assert.Equal(new DateTime(2026, 11, 1), bill.DueDate);
        Assert.Equal("9876543210", bill.AccountHint);
        Assert.Equal("2026-09-23/2026-10-22", bill.BillingPeriod);
        Assert.False(bill.IsPaymentConfirmation);
    }

    [Fact]
    public void ParsesJioPaymentAsEvidenceRatherThanBillReminder()
    {
        var body = "Payment of Rs. 1414.82 has been received on 30-Sep-26\nJio Number: 9876543210\nPayment Mode: UPI Payments";

        var payment = BillMessageParser.Parse(body, new DateTime(2026, 9, 30));

        Assert.NotNull(payment);
        Assert.True(payment!.IsPaymentConfirmation);
        Assert.Equal("Jio", payment.Name);
        Assert.Equal(1414.82m, payment.Amount);
        Assert.Equal(new DateTime(2026, 9, 30), payment.DueDate);
    }

    [Fact]
    public void ParsesHindiUppclReminderAndIgnoresPaymentUrl()
    {
        var body = "\u0915\u0928\u0947\u0915\u094d\u0936\u0928 7041013000 \u092a\u0930 \u092e\u093e\u0939 October-2026 \u0924\u0915 \u0915\u093e \u0915\u0941\u0932 \u092c\u0915\u093e\u092f\u093e: \u20b9 15,788 \u0939\u0948\u0964\n" +
                   "\u0926\u0947\u092f \u0924\u093f\u0925\u093f: 16-10-2026\n\u0935\u093f\u091a\u094d\u091b\u0947\u0926\u0928 \u0924\u093f\u0925\u093f: 23-10-2026\n" +
                   "\u092d\u0941\u0917\u0924\u093e\u0928 https://example.invalid/different-url";

        var bill = BillMessageParser.Parse(body, new DateTime(2026, 10, 1));

        Assert.NotNull(bill);
        Assert.Equal("UPPCL Electricity", bill!.Name);
        Assert.Equal("Utilities / Electricity", bill.Category);
        Assert.Equal(15788m, bill.Amount);
        Assert.Equal("7041013000", bill.AccountHint);
        Assert.Equal("2026-10", bill.BillingPeriod);
        Assert.Equal(new DateTime(2026, 10, 16), bill.DueDate);
        Assert.Equal(new DateTime(2026, 10, 23), bill.DisconnectionDate);
    }
}
