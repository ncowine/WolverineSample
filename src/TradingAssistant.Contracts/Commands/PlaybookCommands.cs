using TradingAssistant.Contracts.DTOs;

namespace TradingAssistant.Contracts.Commands;

/// <summary>
/// Generate strategy templates (playbooks) for a market.
/// Uses Claude if available, falls back to hardcoded templates.
/// </summary>
public record GeneratePlaybooksCommand(string MarketCode);

/// <summary>
/// Query to retrieve strategy templates for a market.
/// </summary>
public record GetTemplatesQuery(string MarketCode);

/// <summary>
/// Result of playbook generation.
/// </summary>
public record GeneratePlaybooksResultDto(
    string MarketCode,
    int TemplatesCreated,
    IReadOnlyList<StrategyV2Dto> Templates);
