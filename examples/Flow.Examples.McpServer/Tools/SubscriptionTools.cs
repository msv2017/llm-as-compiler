using System.ComponentModel;
using Flow.Examples.McpServer.Domain;
using ModelContextProtocol.Server;

namespace Flow.Examples.McpServer.Tools;

[McpServerToolType]
public static class SubscriptionTools
{
    [McpServerTool(Name = "subscriptions.listActive", UseStructuredContent = true)]
    [Description("List a customer's active subscriptions.")]
    public static List<Subscription> ListActive([Description("The customer id")] string customerId) =>
        Store.SubscriptionsByCustomerId.GetValueOrDefault(customerId) ?? new List<Subscription>();

    [McpServerTool(Name = "subscriptions.sendRenewalReminder", UseStructuredContent = true)]
    [Description("Send a renewal reminder for the given subscription id.")]
    public static Ack SendRenewalReminder([Description("The subscription id")] string subscriptionId) =>
        new(true);
}
