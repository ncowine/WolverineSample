using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingAssistant.Contracts.DTOs;
using TradingAssistant.Contracts.Queries;
using TradingAssistant.Domain.Enums;
using TradingAssistant.Infrastructure.Persistence;

namespace TradingAssistant.Application.Handlers.Backtesting;

public static class GetTradeChartDataHandler
{
    public static async Task<TradeChartDataDto> HandleAsync(
        GetTradeChartDataQuery query,
        BacktestDbContext backtestDb,
        MarketDataDbContext marketDb)
    {
        var result = await backtestDb.BacktestResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.BacktestRunId == query.BacktestRunId);

        if (result is null)
            throw new InvalidOperationException($"No result found for backtest run {query.BacktestRunId}");

        // Parse trades from TradeLogJson
        var trades = JsonSerializer.Deserialize<List<TradeLogEntry>>(
            result.TradeLogJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        if (query.TradeIndex < 0 || query.TradeIndex >= trades.Count)
            throw new InvalidOperationException($"Trade index {query.TradeIndex} out of range (0..{trades.Count - 1})");

        var trade = trades[query.TradeIndex];

        // Find the stock by symbol
        var stock = await marketDb.Stocks
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Symbol == trade.Symbol);

        if (stock is null)
            throw new InvalidOperationException($"No market data found for symbol {trade.Symbol}");

        // Load candles around the trade window
        var entryDate = DateTime.Parse(trade.EntryDate);
        var exitDate = DateTime.Parse(trade.ExitDate);

        // Get daily candles in a reasonable window (avoid loading entire history)
        var windowStart = entryDate.AddDays(-(query.BarsBefore * 2)); // rough calendar days
        var windowEnd = exitDate.AddDays(query.BarsAfter * 2);

        var allCandles = await marketDb.PriceCandles
            .AsNoTracking()
            .Where(c => c.StockId == stock.Id
                && c.Interval == CandleInterval.Daily
                && c.Timestamp >= windowStart
                && c.Timestamp <= windowEnd)
            .OrderBy(c => c.Timestamp)
            .Select(c => new { c.Timestamp, c.Open, c.High, c.Low, c.Close, c.Volume })
            .ToListAsync();

        // Find entry/exit indices and expand window
        var entryIdx = allCandles.FindIndex(c => c.Timestamp.Date >= entryDate.Date);
        var exitIdx = allCandles.FindIndex(c => c.Timestamp.Date >= exitDate.Date);

        if (entryIdx < 0) entryIdx = 0;
        if (exitIdx < 0) exitIdx = allCandles.Count - 1;

        var startIdx = Math.Max(0, entryIdx - query.BarsBefore);
        var endIdx = Math.Min(allCandles.Count - 1, exitIdx + query.BarsAfter);

        var windowCandles = allCandles
            .Skip(startIdx)
            .Take(endIdx - startIdx + 1)
            .Select(c => new TradeChartCandle(
                c.Timestamp.ToString("yyyy-MM-dd"),
                c.Open, c.High, c.Low, c.Close, c.Volume))
            .ToList();

        return new TradeChartDataDto(
            Symbol: trade.Symbol,
            Candles: windowCandles,
            EntryPrice: trade.EntryPrice,
            ExitPrice: trade.ExitPrice,
            EntryDate: trade.EntryDate,
            ExitDate: trade.ExitDate,
            ExitReason: trade.ExitReason,
            ReasoningJson: trade.ReasoningJson,
            SignalScore: trade.SignalScore,
            Regime: trade.Regime,
            Shares: trade.Shares,
            PnL: trade.PnL,
            PnLPercent: trade.PnLPercent,
            StopLossPrice: trade.StopLossPrice,
            TakeProfitPrice: trade.TakeProfitPrice,
            HoldingDays: trade.HoldingDays,
            Commission: trade.Commission);
    }

    // Internal DTO for deserializing TradeLogJson entries
    private record TradeLogEntry
    {
        public string Symbol { get; init; } = string.Empty;
        public string EntryDate { get; init; } = string.Empty;
        public decimal EntryPrice { get; init; }
        public string ExitDate { get; init; } = string.Empty;
        public decimal ExitPrice { get; init; }
        public int Shares { get; init; }
        public decimal PnL { get; init; }
        public decimal PnLPercent { get; init; }
        public decimal Commission { get; init; }
        public int HoldingDays { get; init; }
        public string ExitReason { get; init; } = string.Empty;
        public decimal StopLossPrice { get; init; }
        public decimal TakeProfitPrice { get; init; }
        public string? ReasoningJson { get; init; }
        public decimal SignalScore { get; init; }
        public string? Regime { get; init; }
    }
}
