namespace TradingAssistant.Contracts.DTOs;

public record AccountDto(Guid Id, string Name, decimal Balance, string Currency, string AccountType);
