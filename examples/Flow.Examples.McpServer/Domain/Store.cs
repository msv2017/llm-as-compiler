namespace Flow.Examples.McpServer.Domain;

public static class Store
{
    public static readonly Dictionary<string, Customer> CustomersById = new()
    {
        ["cust-1"] = new Customer("cust-1", "Ada Lovelace", "ada@example.com"),
        ["cust-2"] = new Customer("cust-2", "Bob Chen", "bob@example.com"),
        ["cust-6"] = new Customer("cust-6", "Priya Nair", "priya@example.com"),
    };

    public static readonly Dictionary<string, Customer> CustomersByEmail =
        CustomersById.Values.ToDictionary(c => c.Email);

    public static readonly Dictionary<string, string> LoyaltyTierByCustomerId = new()
    {
        ["cust-2"] = "GOLD",
    };

    public static readonly Dictionary<string, List<Invoice>> InvoicesByCustomerId = new()
    {
        ["cust-3"] = new List<Invoice>
        {
            new("inv-1", 120.00m, "UNPAID", new DateTime(2026, 1, 1)),
            new("inv-2", 80.00m, "UNPAID", new DateTime(2026, 2, 1)),
            new("inv-3", 999.00m, "PAID", new DateTime(2026, 3, 1)),
        },
        ["cust-4"] = new List<Invoice>
        {
            new("inv-a", 50.00m, "UNPAID", new DateTime(2026, 1, 1)),
            new("inv-b", 300.00m, "PAID", new DateTime(2026, 2, 1)),
            new("inv-c", 125.50m, "UNPAID", new DateTime(2026, 3, 1)),
        },
        ["cust-5"] = new List<Invoice>
        {
            new("inv-x", 40.00m, "UNPAID", new DateTime(2026, 1, 1)),
        },
    };

    public static readonly Dictionary<string, List<Subscription>> SubscriptionsByCustomerId = new()
    {
        ["cust-7"] = new List<Subscription>
        {
            new("sub-1", "Newsletter"),
            new("sub-2", "Pro Plan"),
            new("sub-3", "Beta Program"),
        },
    };

    public static readonly Dictionary<string, List<Account>> AccountsByCustomerId = new()
    {
        ["cust-8"] = new List<Account>
        {
            new("acct-1", "Personal", "Active"),
            new("acct-2", "Business", "Active"),
        },
    };
}
