using TradingAssistant.Domain.Enums;
using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Aggregated signal from ensemble voting across multiple active strategies.
/// </summary>
public class EnsembleSignal : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public DateTime SignalDate { get; set; }
    public SignalType Direction { get; set; }
    public decimal Confidence { get; set; }

    /// <summary>Voting mode used: "Majority" or "SharpeWeighted".</summary>
    public string VotingMode { get; set; } = "Majority";

    /// <summary>Minimum agreement required (e.g. 2 means at least 2 strategies must agree).</summary>
    public int MinAgreement { get; set; } = 2;

    public int TotalVoters { get; set; }
    public int AgreeingVoters { get; set; }

    /// <summary>JSON array of individual strategy votes.</summary>
    public string VotesJson { get; set; } = "[]";
}
