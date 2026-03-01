namespace TradingAssistant.Contracts.DTOs;

public record PortfolioDto(
    Guid Id,
    Guid AccountId,
    decimal TotalValue,
    decimal CashBalance,
    decimal InvestedValue,
    decimal TotalPnL,
    DateTime LastUpdatedAt,
    string AccountType);
