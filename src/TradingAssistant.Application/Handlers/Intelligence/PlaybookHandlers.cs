using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingAssistant.Application.Handlers.Backtesting;
using TradingAssistant.Application.Intelligence;
using TradingAssistant.Application.Intelligence.Prompts;
using TradingAssistant.Contracts;
using TradingAssistant.Contracts.Backtesting;
using TradingAssistant.Contracts.Commands;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Domain.Backtesting;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Intelligence;

/// <summary>
/// Generates market-specific strategy templates (playbooks).
/// Tries Claude first; falls back to hardcoded PlaybookGenerator templates.
/// Skips any template type that already exists for the market.
/// </summary>
public class GeneratePlaybooksHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task<GeneratePlaybooksResultDto> HandleAsync(
        GeneratePlaybooksCommand command,
        IClaudeClient claude,
        BacktestDbContext backtestDb,
        IntelligenceDbContext intelligenceDb,
        ILogger<GeneratePlaybooksHandler> logger)
    {
        var marketCode = command.MarketCode.Trim().ToUpperInvariant();

        // Check which template types already exist for this market
        var existingTypes = await backtestDb.Strategies
            .Where(s => s.IsTemplate && s.TemplateMarketCode == marketCode)
            .Select(s => s.TemplateType)
            .ToListAsync();

        var missingTypes = PlaybookGenerator.TemplateTypes
            .Where(t => !existingTypes.Contains(t))
            .ToList();

        if (missingTypes.Count == 0)
        {
            logger.LogInformation("All templates already exist for {Market}", marketCode);
            var existing = await GetTemplatesForMarket(backtestDb, marketCode);
            return new GeneratePlaybooksResultDto(marketCode, 0, existing);
        }

        // Load market profile description for Claude prompt enrichment
        var marketProfile = await intelligenceDb.MarketProfiles
            .FirstOrDefaultAsync(p => p.MarketCode == marketCode);
        var marketDescription = marketProfile?.DnaProfileJson ?? marketCode;

        var created = new List<StrategyV2Dto>();

        foreach (var templateType in missingTypes)
        {
            var definition = await TryGenerateWithClaude(claude, marketCode, templateType, marketDescription, logger)
                          ?? GetHardcodedTemplate(marketCode, templateType);

            if (definition is null)
            {
                logger.LogWarning("Failed to generate {Type} template for {Market}", templateType, marketCode);
                continue;
            }

            var regimes = GetRegimesForType(templateType);
            var strategy = new Strategy
            {
                Name = $"[Template] {templateType} — {marketCode}",
                Description = $"Pre-built {templateType.ToLowerInvariant()} strategy template optimized for {marketCode}.",
                IsActive = true,
                RulesJson = JsonSerializer.Serialize(definition, JsonOpts),
                IsTemplate = true,
                TemplateMarketCode = marketCode,
                TemplateType = templateType,
                TemplateRegimes = regimes
            };

            backtestDb.Strategies.Add(strategy);
            created.Add(CreateStrategyV2Handler.MapToDto(strategy, definition));

            logger.LogInformation("Created {Type} template for {Market} (regimes: {Regimes})",
                templateType, marketCode, regimes);
        }

        if (created.Count > 0)
            await backtestDb.SaveChangesAsync();

        return new GeneratePlaybooksResultDto(marketCode, created.Count, created);
    }

    private static async Task<StrategyDefinition?> TryGenerateWithClaude(
        IClaudeClient claude,
        string marketCode,
        string templateType,
        string marketDescription,
        ILogger logger)
    {
        if (claude.IsRateLimited)
        {
            logger.LogInformation("Claude rate-limited, using hardcoded template for {Type}/{Market}",
                templateType, marketCode);
            return null;
        }

        try
        {
            var input = new PlaybookInput(marketCode, templateType, marketDescription);
            var request = new ClaudeRequest(
                PlaybookPrompt.BuildSystemPrompt(),
                PlaybookPrompt.BuildUserPrompt(input),
                Temperature: 0.5m,
                MaxTokens: 2048);

            var response = await claude.CompleteAsync(request);
            if (!response.Success || string.IsNullOrWhiteSpace(response.Content))
            {
                logger.LogWarning("Claude call failed for {Type}/{Market}: {Error}",
                    templateType, marketCode, response.Error);
                return null;
            }

            var definition = PlaybookPrompt.ParseResponse(response.Content);
            if (definition is null)
            {
                logger.LogWarning("Failed to parse Claude response for {Type}/{Market}", templateType, marketCode);
                return null;
            }

            return definition;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Claude exception for {Type}/{Market}", templateType, marketCode);
            return null;
        }
    }

    private static StrategyDefinition? GetHardcodedTemplate(string marketCode, string templateType) =>
        templateType switch
        {
            PlaybookGenerator.Momentum => PlaybookGenerator.BuildMomentum(marketCode),
            PlaybookGenerator.MeanReversion => PlaybookGenerator.BuildMeanReversion(marketCode),
            PlaybookGenerator.Breakout => PlaybookGenerator.BuildBreakout(marketCode),
            _ => null
        };

    private static string GetRegimesForType(string templateType) =>
        templateType switch
        {
            PlaybookGenerator.Momentum => "Bull",
            PlaybookGenerator.MeanReversion => "Sideways",
            PlaybookGenerator.Breakout => "Bull,HighVolatility",
            _ => "Sideways"
        };

    internal static async Task<IReadOnlyList<StrategyV2Dto>> GetTemplatesForMarket(
        BacktestDbContext db, string marketCode)
    {
        var strategies = await db.Strategies
            .Where(s => s.IsTemplate && s.TemplateMarketCode == marketCode)
            .OrderBy(s => s.TemplateType)
            .ToListAsync();

        return strategies.Select(s => CreateStrategyV2Handler.MapToDto(s)).ToList();
    }
}

/// <summary>
/// Returns strategy templates for a specific market.
/// </summary>
public class GetTemplatesHandler
{
    public static async Task<IReadOnlyList<StrategyV2Dto>> HandleAsync(
        GetTemplatesQuery query,
        BacktestDbContext db)
    {
        return await GeneratePlaybooksHandler.GetTemplatesForMarket(
            db, query.MarketCode.Trim().ToUpperInvariant());
    }
}
