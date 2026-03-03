using Microsoft.ML.Data;

namespace TradingAssistant.Infrastructure.ML;

/// <summary>
/// ML.NET prediction output for binary classification.
/// </summary>
public class MlPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}
