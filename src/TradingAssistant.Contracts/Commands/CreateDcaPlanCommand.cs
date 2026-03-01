namespace TradingAssistant.Contracts.Commands;

public record CreateDcaPlanCommand(Guid AccountId, string Symbol, decimal Amount, string Frequency);
