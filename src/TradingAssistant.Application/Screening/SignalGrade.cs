namespace TradingAssistant.Application.Screening;

/// <summary>
/// Confidence grade for a trade signal.
/// </summary>
public enum SignalGrade
{
    A, // 90+
    B, // 75-89
    C, // 60-74
    D, // 40-59
    F  // < 40
}
