namespace TradingAssistant.Contracts.DTOs;

public record MarketProfileDto(
    Guid Id,
    string MarketCode,
    string Exchange,
    string Currency,
    string Timezone,
    string VixSymbol,
    string DataSource,
    bool IsActive,
    string ConfigJson,
    string DnaProfileJson,
    DateTime? DnaProfileUpdatedAt,
    DateTime CreatedAt);

public record CostProfileDto(
    Guid Id,
    string MarketCode,
    string Name,
    decimal CommissionPerShare,
    decimal CommissionPercent,
    decimal ExchangeFeePercent,
    decimal TaxPercent,
    decimal SpreadEstimatePercent,
    bool IsActive,
    DateTime CreatedAt);
