namespace Flow.Examples.McpServer.Domain;

public sealed record Customer(string Id, string Name, string Email);
public sealed record LoyaltyTierResult(string Tier);
public sealed record Invoice(string Id, decimal Amount, string Status, DateTime CreatedAt);
public sealed record Refund(string Id);
public sealed record Subscription(string Id, string Name);
public sealed record Ack(bool Sent);
public sealed record Account(string Id, string Type, string Status);
