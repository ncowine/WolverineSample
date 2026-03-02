namespace TradingAssistant.Contracts.Commands;

public record CreateMarketProfileCommand(
    string MarketCode,
    string Exchange,
    string Currency = "USD",
    string Timezone = "America/New_York",
    string VixSymbol = "",
    string DataSource = "yahoo",
    string ConfigJson = "{}");

public record UpdateMarketProfileCommand(
    Guid ProfileId,
    string? Exchange = null,
    string? Currency = null,
    string? Timezone = null,
    string? VixSymbol = null,
    string? DataSource = null,
    string? ConfigJson = null,
    bool? IsActive = null);

public record CreateCostProfileCommand(
    string MarketCode,
    string Name,
    decimal CommissionPerShare = 0m,
    decimal CommissionPercent = 0m,
    decimal ExchangeFeePercent = 0m,
    decimal TaxPercent = 0m,
    decimal SpreadEstimatePercent = 0m);

public record UpdateCostProfileCommand(
    Guid ProfileId,
    string? Name = null,
    decimal? CommissionPerShare = null,
    decimal? CommissionPercent = null,
    decimal? ExchangeFeePercent = null,
    decimal? TaxPercent = null,
    decimal? SpreadEstimatePercent = null,
    bool? IsActive = null);
