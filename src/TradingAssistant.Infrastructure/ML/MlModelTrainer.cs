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
    /// Detect feature drift by comparing mean/stddev between training window and recent window.
    /// A feature is "significantly drifted" when |mean_shift / training_stddev| > threshold.
    /// </summary>
    public static FeatureDriftReport ComputeFeatureDrift(
        List<FeatureVector> trainingData,
        List<FeatureVector> recentData,
        double driftThreshold = 1.5)
    {
        var entries = new List<FeatureDriftReport.Entry>();

        foreach (var featureName in NumericFeatures)
        {
            var trainValues = ExtractNumericFeature(trainingData, featureName);
            var recentValues = ExtractNumericFeature(recentData, featureName);

            if (trainValues.Length < 5 || recentValues.Length < 5)
                continue;

            var trainMean = trainValues.Average();
            var recentMean = recentValues.Average();
            var trainStd = StdDev(trainValues, trainMean);
            var recentStd = StdDev(recentValues, recentMean);

            // Drift magnitude: normalized mean shift (avoid div by zero)
            var magnitude = trainStd > 1e-9
                ? Math.Abs(recentMean - trainMean) / trainStd
                : (Math.Abs(recentMean - trainMean) > 1e-9 ? double.MaxValue : 0);

            var isSignificant = magnitude > driftThreshold;

            entries.Add(new FeatureDriftReport.Entry(
                featureName, trainMean, recentMean, trainStd, recentStd,
                magnitude, isSignificant));
        }

        return new FeatureDriftReport(
            entries, trainingData.Count, recentData.Count, driftThreshold);
    }

    private static double[] ExtractNumericFeature(List<FeatureVector> data, string featureName)
    {
        return featureName switch
        {
            "smaShort" => data.Select(v => (double)v.SmaShort).ToArray(),
            "smaMedium" => data.Select(v => (double)v.SmaMedium).ToArray(),
            "smaLong" => data.Select(v => (double)v.SmaLong).ToArray(),
            "emaShort" => data.Select(v => (double)v.EmaShort).ToArray(),
            "emaMedium" => data.Select(v => (double)v.EmaMedium).ToArray(),
            "emaLong" => data.Select(v => (double)v.EmaLong).ToArray(),
            "rsi" => data.Select(v => (double)v.Rsi).ToArray(),
            "macdLine" => data.Select(v => (double)v.MacdLine).ToArray(),
            "macdSignal" => data.Select(v => (double)v.MacdSignal).ToArray(),
            "macdHistogram" => data.Select(v => (double)v.MacdHistogram).ToArray(),
            "stochasticK" => data.Select(v => (double)v.StochasticK).ToArray(),
            "stochasticD" => data.Select(v => (double)v.StochasticD).ToArray(),
            "atr" => data.Select(v => (double)v.Atr).ToArray(),
            "bollingerUpper" => data.Select(v => (double)v.BollingerUpper).ToArray(),
            "bollingerMiddle" => data.Select(v => (double)v.BollingerMiddle).ToArray(),
            "bollingerLower" => data.Select(v => (double)v.BollingerLower).ToArray(),
            "bollingerBandwidth" => data.Select(v => (double)v.BollingerBandwidth).ToArray(),
            "bollingerPercentB" => data.Select(v => (double)v.BollingerPercentB).ToArray(),
            "obv" => data.Select(v => (double)v.Obv).ToArray(),
            "volumeMa" => data.Select(v => (double)v.VolumeMa).ToArray(),
            "relativeVolume" => data.Select(v => (double)v.RelativeVolume).ToArray(),
            "closePrice" => data.Select(v => (double)v.ClosePrice).ToArray(),
            "highPrice" => data.Select(v => (double)v.HighPrice).ToArray(),
            "lowPrice" => data.Select(v => (double)v.LowPrice).ToArray(),
            "openPrice" => data.Select(v => (double)v.OpenPrice).ToArray(),
            "dailyRange" => data.Select(v => (double)v.DailyRange).ToArray(),
            "priceToSmaShort" => data.Select(v => (double)v.PriceToSmaShort).ToArray(),
            "priceToSmaLong" => data.Select(v => (double)v.PriceToSmaLong).ToArray(),
            "priceToEmaShort" => data.Select(v => (double)v.PriceToEmaShort).ToArray(),
            "atrPercent" => data.Select(v => (double)v.AtrPercent).ToArray(),
            "regimeConfidence" => data.Select(v => (double)v.RegimeConfidence).ToArray(),
            "daysSinceRegimeChange" => data.Select(v => (double)v.DaysSinceRegimeChange).ToArray(),
            "vixLevel" => data.Select(v => (double)v.VixLevel).ToArray(),
            "breadthScore" => data.Select(v => (double)v.BreadthScore).ToArray(),
            "dayOfWeek" => data.Select(v => (double)v.DayOfWeek).ToArray(),
            "winStreak" => data.Select(v => (double)v.WinStreak).ToArray(),
            "lossStreak" => data.Select(v => (double)v.LossStreak).ToArray(),
            "recentWinRate" => data.Select(v => (double)v.RecentWinRate).ToArray(),
            "portfolioHeat" => data.Select(v => (double)v.PortfolioHeat).ToArray(),
            "openPositionCount" => data.Select(v => (double)v.OpenPositionCount).ToArray(),
            "tradePrice" => data.Select(v => (double)v.TradePrice).ToArray(),
            _ => Array.Empty<double>()
        };
    }

    private static double StdDev(double[] values, double mean)
    {
        if (values.Length < 2) return 0;
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (values.Length - 1));
    }

    /// <summary>
    /// Feature drift analysis report.
    /// </summary>
    public class FeatureDriftReport
    {
        public List<Entry> Entries { get; }
        public int TrainingWindowSize { get; }
        public int RecentWindowSize { get; }
        public double Threshold { get; }

        public bool HasSignificantDrift => Entries.Any(e => e.IsSignificant);
        public List<Entry> SignificantEntries => Entries.Where(e => e.IsSignificant).ToList();

        public FeatureDriftReport(
            List<Entry> entries, int trainingWindowSize, int recentWindowSize, double threshold)
        {
            Entries = entries;
            TrainingWindowSize = trainingWindowSize;
            RecentWindowSize = recentWindowSize;
            Threshold = threshold;
        }

        public record Entry(
            string FeatureName,
            double TrainingMean,
            double RecentMean,
            double TrainingStdDev,
            double RecentStdDev,
            double DriftMagnitude,
            bool IsSignificant);
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
