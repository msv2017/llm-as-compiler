using System.ComponentModel;
using Flow.Examples.McpServer.Domain;
using ModelContextProtocol.Server;

namespace Flow.Examples.McpServer.Tools;

[McpServerToolType]
public static class BillingTools
{
    [McpServerTool(Name = "billing.listInvoices", UseStructuredContent = true)]
    [Description("List a customer's invoices.")]
    public static List<Invoice> ListInvoices([Description("The customer id")] string customerId) =>
        Store.InvoicesByCustomerId.GetValueOrDefault(customerId) ?? new List<Invoice>();

    [McpServerTool(Name = "billing.createRefund", UseStructuredContent = true)]
    [Description("Create a refund for the given invoice id.")]
    public static Refund CreateRefund([Description("The invoice id to refund")] string invoiceId) =>
        new($"refund-{invoiceId}");
}
