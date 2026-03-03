namespace TradingAssistant.Domain.Intelligence.Enums;

/// <summary>
/// Primary reason a strategy lost money during a period.
/// Classified by Claude during strategy autopsy.
/// </summary>
public enum LossReason
{
    RegimeMismatch,
    SignalDegradation,
    BlackSwan,
    PositionSizingError,
    StopLossFailure
}
