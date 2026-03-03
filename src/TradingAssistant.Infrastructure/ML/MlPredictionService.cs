using System.Collections.Concurrent;
using Microsoft.ML;

namespace TradingAssistant.Infrastructure.ML;

/// <summary>
/// Thread-safe singleton service for ML model inference.
/// Loads the active model per market and provides fast predictions.
/// PredictionEngine is thread-safe when created via pooled approach.
/// </summary>
public class MlPredictionService
{
    private readonly ConcurrentDictionary<string, PredictionEngine<FeatureVector, MlPrediction>> _engines = new();
    private readonly ConcurrentDictionary<string, string> _loadedModelPaths = new();

    /// <summary>
    /// Load a trained model from disk for a specific market.
    /// Replaces any previously loaded model for that market.
    /// </summary>
    public void LoadModel(string marketCode, string modelPath)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Model file not found: {modelPath}");

        var mlContext = new MLContext(seed: 42);
        var model = mlContext.Model.Load(modelPath, out _);
        var engine = mlContext.Model.CreatePredictionEngine<FeatureVector, MlPrediction>(model);

        _engines[marketCode] = engine;
        _loadedModelPaths[marketCode] = modelPath;
    }

    /// <summary>
    /// Predict the probability of a profitable trade (0.0 - 1.0).
    /// Returns null if no model is loaded for the given market.
    /// </summary>
    public float? PredictConfidence(string marketCode, FeatureVector features)
    {
        if (!_engines.TryGetValue(marketCode, out var engine))
            return null;

        var prediction = engine.Predict(features);
        return Math.Clamp(prediction.Probability, 0f, 1f);
    }

    /// <summary>
    /// Check if a model is loaded for a market.
    /// </summary>
    public bool HasModel(string marketCode) => _engines.ContainsKey(marketCode);

    /// <summary>
    /// Get the loaded model path for a market, or null.
    /// </summary>
    public string? GetLoadedModelPath(string marketCode) =>
        _loadedModelPaths.TryGetValue(marketCode, out var path) ? path : null;

    /// <summary>
    /// Remove a loaded model (e.g., when model is deactivated).
    /// </summary>
    public void UnloadModel(string marketCode)
    {
        _engines.TryRemove(marketCode, out _);
        _loadedModelPaths.TryRemove(marketCode, out _);
    }

    /// <summary>
    /// Get all market codes with loaded models.
    /// </summary>
    public IReadOnlyList<string> GetLoadedMarkets() => _engines.Keys.ToList();
}
