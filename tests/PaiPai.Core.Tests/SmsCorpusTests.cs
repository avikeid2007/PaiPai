using System.Text.Json;
using PaiPai.Core;
using Xunit;
using Xunit.Abstractions;

namespace PaiPai.Core.Tests;

public class SmsCorpusTests
{
    readonly ITestOutputHelper _output;

    public SmsCorpusTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Corpus_parser_has_no_transaction_field_mismatches()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "IndianPaymentSmsCorpus",
            "indian-payment-sms-dataset.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var rows = document.RootElement.GetProperty("rows").EnumerateArray();
        var mismatches = new List<string>();
        var count = 0;

        foreach (var row in rows)
        {
            count++;
            var id = row.GetProperty("id").GetString() ?? "unknown";
            var body = row.GetProperty("raw_sms").GetString() ?? "";
            var expected = row.GetProperty("expected");
            var expectedBookable = expected.GetProperty("bookable").GetBoolean();
            var expectedAmount = expected.GetProperty("transaction_amount_paise");
            var shouldParse = expectedAmount.ValueKind != JsonValueKind.Null;
            var classification = SmsParser.Classify(body);
            var actual = classification.Transaction;

            if (actual is null)
            {
                if (shouldParse)
                    mismatches.Add($"{id}: expected a completed transaction, parser returned {classification.Disposition} ({classification.Reason})");
                continue;
            }

            if (!shouldParse)
            {
                mismatches.Add($"{id}: expected no completed transaction, parsed {actual.Direction} {actual.Amount}");
                continue;
            }

            var expectedAmountPaise = expectedAmount.GetInt64();
            var actualAmountPaise = decimal.ToInt64(actual.Amount * 100m);
            if (actualAmountPaise != expectedAmountPaise)
                mismatches.Add($"{id}: amount {actualAmountPaise} paise, expected {expectedAmountPaise}");

            var expectedDirection = expected.GetProperty("direction").GetString();
            var normalizedExpectedDirection = string.Equals(expectedDirection, "reversal", StringComparison.OrdinalIgnoreCase)
                ? Direction.Credit.ToString()
                : expectedDirection;
            if (!string.Equals(actual.Direction.ToString(), normalizedExpectedDirection, StringComparison.OrdinalIgnoreCase))
                mismatches.Add($"{id}: direction {actual.Direction}, expected {expectedDirection}");
            if (string.Equals(expectedDirection, "reversal", StringComparison.OrdinalIgnoreCase) &&
                classification.Disposition != SmsMessageDisposition.NeedsReview)
                mismatches.Add($"{id}: reversal should remain in review, parser returned {classification.Disposition}");
            if (expectedBookable && actual.Direction != Direction.Debit)
                mismatches.Add($"{id}: corpus marks it as an expense, parser returned {actual.Direction}");

            var expectedMerchant = GetOptionalString(expected, "merchant");
            if (expectedMerchant is not null &&
                !string.Equals(actual.Merchant, expectedMerchant, StringComparison.OrdinalIgnoreCase))
                mismatches.Add($"{id}: merchant '{actual.Merchant}', expected '{expectedMerchant}'");

            var expectedLast4 = GetOptionalString(expected, "account_last4") ?? GetOptionalString(expected, "card_last4");
            if (expectedLast4 is not null && actual.AccountHint != expectedLast4)
                mismatches.Add($"{id}: account/card suffix '{actual.AccountHint}', expected '{expectedLast4}'");

            var expectedReference = GetOptionalString(expected, "reference");
            if (expectedReference is not null && actual.Reference != expectedReference)
                mismatches.Add($"{id}: reference '{actual.Reference}', expected '{expectedReference}'");
        }

        Assert.Equal(172, count);
        var rowsWithMismatches = mismatches
            .Select(mismatch => mismatch.Split(':', 2)[0])
            .Distinct(StringComparer.Ordinal)
            .Count();
        _output.WriteLine($"Corpus rows: {count}; rows with mismatches: {rowsWithMismatches}; field/decision mismatches: {mismatches.Count}.");
        foreach (var mismatch in mismatches) _output.WriteLine(mismatch);

        Assert.True(mismatches.Count == 0,
            $"Parser produced {mismatches.Count} corpus mismatches.{Environment.NewLine}{string.Join(Environment.NewLine, mismatches.Take(200))}");
    }

    static string? GetOptionalString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        return value.GetString();
    }
}
