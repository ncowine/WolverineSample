using TradingAssistant.SharedKernel;

namespace TradingAssistant.Domain.Intelligence;

/// <summary>
/// Metadata for a trained ML model. Model file stored on filesystem.
/// </summary>
public class MlModel : BaseEntity
{
    public string MarketCode { get; set; } = string.Empty;
    public int ModelVersion { get; set; }
    public int FeatureVersion { get; set; }
    public string ModelPath { get; set; } = string.Empty;
    public DateTime TrainedAt { get; set; }

    // Training metrics
    public double Auc { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double Accuracy { get; set; }

    // Training data info
    public int TrainingSamples { get; set; }
    public int ValidationSamples { get; set; }
    public int WinSamples { get; set; }
    public int LossSamples { get; set; }

    // Feature importance (top 10 as JSON)
    public string FeatureImportanceJson { get; set; } = string.Empty;

    // Status
    public bool IsActive { get; set; }
    public string? DeactivationReason { get; set; }
}
