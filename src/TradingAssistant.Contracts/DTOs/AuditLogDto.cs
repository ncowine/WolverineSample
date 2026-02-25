namespace TradingAssistant.Contracts.DTOs;

public record AuditLogDto(
    Guid Id,
    string EntityType,
    string EntityId,
    string Action,
    string? OldValues,
    string? NewValues,
    Guid? UserId,
    DateTime Timestamp);
