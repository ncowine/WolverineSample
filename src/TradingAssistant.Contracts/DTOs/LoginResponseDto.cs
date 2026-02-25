namespace TradingAssistant.Contracts.DTOs;

public record LoginResponseDto(string Token, DateTime ExpiresAt);
