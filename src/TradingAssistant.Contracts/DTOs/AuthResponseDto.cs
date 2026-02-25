namespace TradingAssistant.Contracts.DTOs;

public record AuthResponseDto(Guid UserId, string Email, string Role, Guid AccountId);
