using System.ComponentModel;
using Flow.Examples.McpServer.Domain;
using ModelContextProtocol.Server;

namespace Flow.Examples.McpServer.Tools;

[McpServerToolType]
public static class CrmTools
{
    [McpServerTool(Name = "crm.getCustomerById", UseStructuredContent = true)]
    [Description("Look up a customer by id. The id is assumed to exist.")]
    public static Customer GetCustomerById([Description("The customer id")] string id) =>
        Store.CustomersById[id];

    [McpServerTool(Name = "crm.findCustomerById", UseStructuredContent = true)]
    [Description("Look up a customer by id. Returns null if no customer has that id.")]
    public static Customer? FindCustomerById([Description("The customer id")] string id) =>
        Store.CustomersById.GetValueOrDefault(id);

    [McpServerTool(Name = "crm.getCustomerByEmail", UseStructuredContent = true)]
    [Description("Look up a customer by email address. The email is assumed to exist.")]
    public static Customer GetCustomerByEmail([Description("The customer's email address")] string email) =>
        Store.CustomersByEmail[email];

    [McpServerTool(Name = "crm.getLoyaltyTier", UseStructuredContent = true)]
    [Description("Look up a customer's loyalty tier by customer id.")]
    public static LoyaltyTierResult GetLoyaltyTier([Description("The customer id")] string customerId) =>
        new(Store.LoyaltyTierByCustomerId[customerId]);

    [McpServerTool(Name = "crm.listAccounts", UseStructuredContent = true)]
    [Description("List a customer's accounts.")]
    public static List<Account> ListAccounts([Description("The customer id")] string customerId) =>
        Store.AccountsByCustomerId.GetValueOrDefault(customerId) ?? new List<Account>();
}
