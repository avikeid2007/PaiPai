namespace PaiPai.Core;

public static class Categorizer
{
    public static readonly string[] Categories =
        { "Food", "Transport", "Shopping", "Bills", "Rent", "Health", "Entertainment", "Other" };

    static readonly (string Category, string[] Keys)[] Builtin =
    {
        ("Food", new[] { "swiggy", "zomato", "restaurant", "cafe", "chai", "bakery", "dominos", "mcdonald" }),
        ("Transport", new[] { "uber", "ola", "rapido", "irctc", "metro", "petrol", "fuel", "fastag" }),
        ("Shopping", new[] { "amazon", "flipkart", "myntra", "ajio", "meesho" }),
        ("Bills", new[] { "electric", "airtel", "jio", "recharge", "insurance", "broadband", "gas" }),
        ("Rent", new[] { "rent", "landlord" }),
        ("Health", new[] { "pharmacy", "apollo", "hospital", "clinic", "medic" }),
        ("Entertainment", new[] { "netflix", "spotify", "hotstar", "bookmyshow", "prime" }),
    };

    /// <summary>User-learned merchant rules win over the built-in keywords.</summary>
    public static string Categorize(string merchant, IReadOnlyDictionary<string, string>? userRules = null)
    {
        var m = merchant.ToLowerInvariant();
        if (userRules != null)
            foreach (var (key, cat) in userRules)
                if (m.Contains(key.ToLowerInvariant())) return cat;
        foreach (var (cat, keys) in Builtin)
            if (keys.Any(k => m.Contains(k))) return cat;
        return "Other";
    }
}
