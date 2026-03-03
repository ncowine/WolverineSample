using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Intelligence;
using TradingAssistant.Domain.Intelligence.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

public class ReviewTradeHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<ReviewTradeResultDto> HandleAsync(
        ReviewTradeCommand command,
        IClaudeClient claude,
        IntelligenceDbContext intelligenceDb,
        ILogger<ReviewTradeHandler> logger)
    {
        logger.LogInformation("Reviewing trade {TradeId} for {Symbol} ({Market})",
            command.TradeId, command.Symbol, command.MarketCode);

        // Check for existing review
        var existing = await intelligenceDb.TradeReviews
            .AnyAsync(r => r.TradeId == command.TradeId);
        if (existing)
        {
            return new ReviewTradeResultDto(
                Success: false, ReviewId: null, OutcomeClass: null,
                MistakeType: null, Score: 0, Summary: string.Empty,
                Error: $"Trade '{command.TradeId}' has already been reviewed.");
        }

        // Check rate limit
        if (claude.IsRateLimited)
        {
            return new ReviewTradeResultDto(
                Success: false, ReviewId: null, OutcomeClass: null,
                MistakeType: null, Score: 0, Summary: string.Empty,
                Error: "Claude API daily rate limit reached. Try again tomorrow.");
        }

        // Load indicator values from FeatureSnapshot if available
        Dictionary<string, decimal>? indicators = null;
        var snapshot = await intelligenceDb.FeatureSnapshots
            .FirstOrDefaultAsync(s => s.TradeId == command.TradeId);
        if (snapshot is not null && !string.IsNullOrWhiteSpace(snapshot.FeaturesJson))
        {
            indicators = TryDecompressFeatures(snapshot.FeaturesJson);
        }

        // Build prompt
        var side = command.PnlPercent >= 0 ? "Long" : "Long"; // default; side could be inferred from price direction
        var input = new TradeReviewInput(
            Symbol: command.Symbol,
            Side: command.EntryPrice <= command.ExitPrice ? "Long" : "Short",
            EntryDate: command.EntryDate,
            ExitDate: command.ExitDate,
            EntryPrice: command.EntryPrice,
            ExitPrice: command.ExitPrice,
            PnlPercent: command.PnlPercent,
            StrategyName: command.StrategyName,
            RegimeAtEntry: command.RegimeAtEntry,
            RegimeAtExit: command.RegimeAtExit,
            Grade: command.Grade,
            MlConfidence: command.MlConfidence,
            IndicatorValuesAtEntry: indicators);

        var request = new ClaudeRequest(
            TradeReviewPrompt.BuildSystemPrompt(),
            TradeReviewPrompt.BuildUserPrompt(input),
            Temperature: 0.3m,
            MaxTokens: 2048);

        var response = await claude.CompleteAsync(request);
        if (!response.Success)
        {
            logger.LogWarning("Claude trade review call failed: {Error}", response.Error);
            return new ReviewTradeResultDto(
                Success: false, ReviewId: null, OutcomeClass: null,
                MistakeType: null, Score: 0, Summary: string.Empty,
                Error: $"Claude API error: {response.Error}");
        }

        // Parse response
        var output = TradeReviewPrompt.ParseResponse(response.Content);
        if (output is null)
        {
            logger.LogWarning("Failed to parse Claude trade review response");
            return new ReviewTradeResultDto(
                Success: false, ReviewId: null, OutcomeClass: null,
                MistakeType: null, Score: 0, Summary: string.Empty,
                Error: "Failed to parse Claude's trade review response.");
        }

        // Classify outcome and mistake type
        var outcomeClass = ClassifyOutcome(output.OutcomeClass);
        var mistakeType = command.PnlPercent < 0 ? ClassifyMistake(output.MistakeType) : (MistakeType?)null;

        var durationHours = (command.ExitDate - command.EntryDate).TotalHours;

        // Save review
        var review = new TradeReview
        {
            TradeId = command.TradeId,
            Symbol = command.Symbol,
            MarketCode = command.MarketCode,
            StrategyName = command.StrategyName,
            EntryPrice = command.EntryPrice,
            ExitPrice = command.ExitPrice,
            EntryDate = command.EntryDate,
            ExitDate = command.ExitDate,
            PnlPercent = command.PnlPercent,
            PnlAbsolute = command.PnlAbsolute,
            DurationHours = durationHours,
            RegimeAtEntry = command.RegimeAtEntry,
            RegimeAtExit = command.RegimeAtExit,
            Grade = command.Grade,
            MlConfidence = command.MlConfidence,
            IndicatorValuesJson = indicators is not null
                ? JsonSerializer.Serialize(indicators, JsonOpts)
                : "{}",
            OutcomeClass = outcomeClass,
            MistakeType = mistakeType,
            Score = Math.Clamp(output.Score, 1, 10),
            StrengthsJson = JsonSerializer.Serialize(output.Strengths, JsonOpts),
            WeaknessesJson = JsonSerializer.Serialize(output.Weaknesses, JsonOpts),
            LessonsLearnedJson = JsonSerializer.Serialize(output.LessonsLearned, JsonOpts),
            Summary = output.Summary
        };

        intelligenceDb.TradeReviews.Add(review);
        await intelligenceDb.SaveChangesAsync();

        logger.LogInformation(
            "Trade review completed for {Symbol}: OutcomeClass={Outcome}, Score={Score}",
            command.Symbol, outcomeClass, output.Score);

        return new ReviewTradeResultDto(
            Success: true,
            ReviewId: review.Id,
            OutcomeClass: outcomeClass.ToString(),
            MistakeType: mistakeType?.ToString(),
            Score: review.Score,
            Summary: review.Summary);
    }

    internal static OutcomeClass ClassifyOutcome(string claudeOutcome)
    {
        if (string.IsNullOrWhiteSpace(claudeOutcome))
            return OutcomeClass.BadEntry;

        if (Enum.TryParse<OutcomeClass>(claudeOutcome.Trim(), ignoreCase: true, out var parsed))
            return parsed;

        var lower = claudeOutcome.ToLowerInvariant();
        if (lower.Contains("good entry") && lower.Contains("good exit"))
            return OutcomeClass.GoodEntryGoodExit;
        if (lower.Contains("good entry") && lower.Contains("bad exit"))
            return OutcomeClass.GoodEntryBadExit;
        if (lower.Contains("regime") || lower.Contains("mismatch"))
            return OutcomeClass.RegimeMismatch;
        if (lower.Contains("premature") || lower.Contains("early"))
            return OutcomeClass.StoppedPrematurely;
        if (lower.Contains("stopped") || lower.Contains("correct"))
            return OutcomeClass.StoppedCorrectly;

        return OutcomeClass.BadEntry;
    }

    internal static MistakeType ClassifyMistake(string? claudeMistake)
    {
        if (string.IsNullOrWhiteSpace(claudeMistake))
            return Domain.Intelligence.Enums.MistakeType.BadSignal;

        if (Enum.TryParse<MistakeType>(claudeMistake.Trim(), ignoreCase: true, out var parsed))
            return parsed;

        var lower = claudeMistake.ToLowerInvariant();
        if (lower.Contains("timing"))
            return Domain.Intelligence.Enums.MistakeType.BadTiming;
        if (lower.Contains("regime") || lower.Contains("mismatch"))
            return Domain.Intelligence.Enums.MistakeType.RegimeMismatch;
        if (lower.Contains("tight"))
            return Domain.Intelligence.Enums.MistakeType.StopTooTight;
        if (lower.Contains("loose"))
            return Domain.Intelligence.Enums.MistakeType.StopTooLoose;
        if (lower.Contains("oversiz") || lower.Contains("position"))
            return Domain.Intelligence.Enums.MistakeType.OversizedPosition;
        if (lower.Contains("correlat"))
            return Domain.Intelligence.Enums.MistakeType.CorrelatedLoss;
        if (lower.Contains("black swan") || lower.Contains("unforesee"))
            return Domain.Intelligence.Enums.MistakeType.BlackSwan;

        return Domain.Intelligence.Enums.MistakeType.BadSignal;
    }

    private static Dictionary<string, decimal>? TryDecompressFeatures(string featuresJson)
    {
        try
        {
            // FeatureSnapshots store compressed Base64 JSON — try plain JSON first
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(featuresJson, JsonOpts);
        }
        catch
        {
            try
            {
                // Try Base64+GZip decompression
                var compressed = Convert.FromBase64String(featuresJson);
                using var ms = new System.IO.MemoryStream(compressed);
                using var gzip = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var reader = new System.IO.StreamReader(gzip);
                var json = reader.ReadToEnd();
                return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOpts);
            }
            catch
            {
                return null;
            }
        }
    }
}
