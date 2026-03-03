using System.Text.Json;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace TradingAssistant.Infrastructure.ML;

/// <summary>
/// Trains binary classification models using ML.NET + LightGBM.
/// Walk-forward training: train on first N, validate on next M (no future leakage).
/// </summary>
public class MlModelTrainer
{
    public const double MinimumAuc = 0.55;
    public const int DefaultNumLeaves = 31;
    public const double DefaultLearningRate = 0.1;
    public const int DefaultNumIterations = 100;
    public const double DefaultTrainSplitRatio = 0.8;

    private static readonly string[] NumericFeatures =
    [
        "smaShort", "smaMedium", "smaLong", "emaShort", "emaMedium", "emaLong",
        "rsi", "macdLine", "macdSignal", "macdHistogram", "stochasticK", "stochasticD",
        "atr", "bollingerUpper", "bollingerMiddle", "bollingerLower",
        "bollingerBandwidth", "bollingerPercentB",
        "obv", "volumeMa", "relativeVolume",
        "closePrice", "highPrice", "lowPrice", "openPrice", "dailyRange",
        "priceToSmaShort", "priceToSmaLong", "priceToEmaShort", "atrPercent",
        "regimeConfidence", "daysSinceRegimeChange", "vixLevel", "breadthScore",
        "dayOfWeek",
        "winStreak", "lossStreak", "recentWinRate", "portfolioHeat",
        "openPositionCount", "tradePrice"
    ];

    private static readonly string[] CategoricalFeatures =
        ["regimeLabel", "marketCode", "symbol", "orderSide"];

    /// <summary>
    /// Train a binary classification model from FeatureVectors.
    /// Uses walk-forward split: first trainRatio% for training, rest for validation.
    /// </summary>
    public TrainResult Train(
        List<FeatureVector> data,
        double trainSplitRatio = DefaultTrainSplitRatio,
        int numLeaves = DefaultNumLeaves,
        double learningRate = DefaultLearningRate,
        int numIterations = DefaultNumIterations)
    {
        if (data.Count < 20)
        {
            return new TrainResult
            {
                Success = false,
                FailureReason = $"Insufficient data: {data.Count} samples (minimum 20)"
            };
        }

        var mlContext = new MLContext(seed: 42);

        // Walk-forward split: chronological, no shuffling
        var splitIndex = (int)(data.Count * trainSplitRatio);
        var trainData = data.Take(splitIndex).ToList();
        var validData = data.Skip(splitIndex).ToList();

        if (validData.Count < 5)
        {
            return new TrainResult
            {
                Success = false,
                FailureReason = $"Insufficient validation data: {validData.Count} samples"
            };
        }

        var trainView = mlContext.Data.LoadFromEnumerable(trainData);
        var validView = mlContext.Data.LoadFromEnumerable(validData);

        // Build pipeline: encode categoricals → concat all features → LightGBM
        var pipeline = BuildPipeline(mlContext, numLeaves, learningRate, numIterations);

        // Train
        var model = pipeline.Fit(trainView);

        // Evaluate on validation set
        var predictions = model.Transform(validView);
        var metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: "label");

        // Feature importance
        var featureImportance = ComputeFeatureImportance(
            mlContext, model, validView);

        var result = new TrainResult
        {
            Success = true,
            Model = model,
            MlContext = mlContext,
            Auc = metrics.AreaUnderRocCurve,
            Precision = metrics.PositivePrecision,
            Recall = metrics.PositiveRecall,
            F1Score = metrics.F1Score,
            Accuracy = metrics.Accuracy,
            TrainingSamples = trainData.Count,
            ValidationSamples = validData.Count,
            WinSamples = data.Count(v => v.Label),
            LossSamples = data.Count(v => !v.Label),
            TopFeatures = featureImportance,
            MeetsMinimumAuc = metrics.AreaUnderRocCurve >= MinimumAuc
        };

        return result;
    }

    /// <summary>
    /// Save a trained model to disk as .zip file.
    /// Creates directories if they don't exist.
    /// </summary>
    public void SaveModel(TrainResult result, string path)
    {
        if (result.Model is null || result.MlContext is null)
            throw new InvalidOperationException("Cannot save: model not trained");

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        result.MlContext.Model.Save(result.Model, null, path);
    }

    /// <summary>
    /// Load a trained model from disk.
    /// </summary>
    public (MLContext Context, ITransformer Model) LoadModel(string path)
    {
        var mlContext = new MLContext(seed: 42);
        var model = mlContext.Model.Load(path, out _);
        return (mlContext, model);
    }

    private static IEstimator<ITransformer> BuildPipeline(
        MLContext mlContext, int numLeaves, double learningRate, int numIterations)
    {
        // Concatenate all features
        var allFeatures = NumericFeatures
            .Concat(["regimeLabelEncoded", "marketCodeEncoded", "symbolEncoded", "orderSideEncoded"])
            .ToArray();

        // OneHot encode categorical features → Concat → LightGBM
        return mlContext.Transforms.Categorical.OneHotEncoding(
                outputColumnName: "regimeLabelEncoded", inputColumnName: "regimeLabel")
            .Append(mlContext.Transforms.Categorical.OneHotEncoding(
                outputColumnName: "marketCodeEncoded", inputColumnName: "marketCode"))
            .Append(mlContext.Transforms.Categorical.OneHotEncoding(
                outputColumnName: "symbolEncoded", inputColumnName: "symbol"))
            .Append(mlContext.Transforms.Categorical.OneHotEncoding(
                outputColumnName: "orderSideEncoded", inputColumnName: "orderSide"))
            .Append(mlContext.Transforms.Concatenate("Features", allFeatures))
            .Append(mlContext.BinaryClassification.Trainers.LightGbm(
                labelColumnName: "label",
                featureColumnName: "Features",
                numberOfLeaves: numLeaves,
                learningRate: learningRate,
                numberOfIterations: numIterations));
    }

    internal static List<FeatureImportanceEntry> ComputeFeatureImportance(
        MLContext mlContext, ITransformer model, IDataView validationData)
    {
        try
        {
            var transformed = model.Transform(validationData);
            var lastTransformer = (model as TransformerChain<ITransformer>)?.LastTransformer;

            if (lastTransformer is not ISingleFeaturePredictionTransformer<object> predictor)
                return [];

            var pfi = mlContext.BinaryClassification
                .PermutationFeatureImportance(predictor, transformed,
                    labelColumnName: "label", permutationCount: 3);

            // Map slot indices back to feature names
            var featureColumn = transformed.Schema["Features"];
            var slotNames = new VBuffer<ReadOnlyMemory<char>>();
            featureColumn.GetSlotNames(ref slotNames);
            var names = slotNames.DenseValues().Select(v => v.ToString()).ToArray();

            var entries = pfi
                .Select((metric, idx) => new FeatureImportanceEntry(
                    idx < names.Length ? names[idx] : $"Feature_{idx}",
                    Math.Abs(metric.AreaUnderRocCurve.Mean)))
                .OrderByDescending(e => e.Importance)
                .Take(10)
                .ToList();

            return entries;
        }
        catch
        {
            // PFI can fail with certain data distributions; return empty
            return [];
        }
    }

    /// <summary>
    /// Result of a training run.
    /// </summary>
    public class TrainResult
    {
        public bool Success { get; set; }
        public string? FailureReason { get; set; }

        // Model artifacts
        public ITransformer? Model { get; set; }
        public MLContext? MlContext { get; set; }

        // Metrics
        public double Auc { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public double Accuracy { get; set; }

        // Data stats
        public int TrainingSamples { get; set; }
        public int ValidationSamples { get; set; }
        public int WinSamples { get; set; }
        public int LossSamples { get; set; }

        // Feature importance
        public List<FeatureImportanceEntry> TopFeatures { get; set; } = [];

        public bool MeetsMinimumAuc { get; set; }

        public string TopFeaturesJson =>
            JsonSerializer.Serialize(TopFeatures,
                new JsonSerializerOptions { WriteIndented = false });
    }

    public record FeatureImportanceEntry(string Name, double Importance);
}
