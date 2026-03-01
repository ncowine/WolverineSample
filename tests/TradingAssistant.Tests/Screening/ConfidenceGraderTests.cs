using TradingAssistant.Application.Screening;

namespace TradingAssistant.Tests.Screening;

public class ConfidenceGraderTests
{
    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Build a signal evaluation with configurable confirmation pass/fail.
    /// </summary>
    private static SignalEvaluation MakeEvaluation(
        bool trendPass = true,
        bool momentumPass = true,
        bool volumePass = true,
        bool volatilityPass = true,
        bool macdPass = true,
        bool stochPass = true,
        SignalDirection direction = SignalDirection.Long)
    {
        var confirmations = new List<ConfirmationResult>
        {
            new() { Name = "TrendAlignment", Passed = trendPass, Weight = 1m, Reason = trendPass ? "Trend aligns" : "Trend opposes" },
            new() { Name = "Momentum", Passed = momentumPass, Weight = 1m, Reason = "RSI check" },
            new() { Name = "Volume", Passed = volumePass, Weight = 1m, Reason = volumePass ? "Volume above 1.2x" : "Volume below threshold" },
            new() { Name = "Volatility", Passed = volatilityPass, Weight = 1m, Reason = volatilityPass ? "ATR normal" : "ATR abnormal" },
            new() { Name = "MacdHistogram", Passed = macdPass, Weight = 1m, Reason = "MACD check" },
            new() { Name = "Stochastic", Passed = stochPass, Weight = 1m, Reason = "Stochastic check" }
        };

        var totalWeight = confirmations.Sum(c => c.Weight);
        var passedWeight = confirmations.Where(c => c.Passed).Sum(c => c.Weight);

        return new SignalEvaluation
        {
            Symbol = "AAPL",
            Date = new DateTime(2025, 6, 15),
            Direction = direction,
            Confirmations = confirmations,
            TotalScore = totalWeight > 0 ? passedWeight / totalWeight : 0m
        };
    }

    // ── Grade Assignment ─────────────────────────────────────

    [Theory]
    [InlineData(100, SignalGrade.A)]
    [InlineData(95, SignalGrade.A)]
    [InlineData(90, SignalGrade.A)]
    [InlineData(89.99, SignalGrade.B)]
    [InlineData(75, SignalGrade.B)]
    [InlineData(74.99, SignalGrade.C)]
    [InlineData(60, SignalGrade.C)]
    [InlineData(59.99, SignalGrade.D)]
    [InlineData(40, SignalGrade.D)]
    [InlineData(39.99, SignalGrade.F)]
    [InlineData(0, SignalGrade.F)]
    public void Grade_thresholds_correct(decimal score, SignalGrade expected)
    {
        Assert.Equal(expected, ConfidenceGrader.AssignGrade(score));
    }

    // ── Perfect Signal → A Grade ─────────────────────────────

    [Fact]
    public void Perfect_signal_gets_A_grade()
    {
        var eval = MakeEvaluation(); // all pass
        // Long: entry 100, stop 95 (risk=5), target 115 (reward=15) → R:R=3.0
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 115m, historicalWinRate: 80m);

        Assert.Equal(SignalGrade.A, report.Grade);
        Assert.True(report.Score >= 90m, $"Expected >= 90, got {report.Score}");
        Assert.True(report.PassesScreener);
        Assert.Equal("AAPL", report.Symbol);
        Assert.Equal(SignalDirection.Long, report.Direction);
    }

    // ── Worst Signal → F Grade ───────────────────────────────

    [Fact]
    public void Worst_signal_gets_F_grade()
    {
        var eval = MakeEvaluation(
            trendPass: false, momentumPass: false, volumePass: false,
            volatilityPass: false, macdPass: false, stochPass: false);
        // Poor R:R (1:0.5), no history
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 102.5m, historicalWinRate: 20m);

        Assert.Equal(SignalGrade.F, report.Grade);
        Assert.True(report.Score < 40m);
        Assert.False(report.PassesScreener);
    }

    // ── Only A and B Pass Screener ───────────────────────────

    [Theory]
    [InlineData(SignalGrade.A, true)]
    [InlineData(SignalGrade.B, true)]
    [InlineData(SignalGrade.C, false)]
    [InlineData(SignalGrade.D, false)]
    [InlineData(SignalGrade.F, false)]
    public void Only_A_and_B_pass_screener(SignalGrade grade, bool expected)
    {
        var report = new SignalReport { Grade = grade };
        Assert.Equal(expected, report.PassesScreener);
    }

    // ── Breakdown Has All 6 Factors ──────────────────────────

    [Fact]
    public void Breakdown_contains_6_factors()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 110m);

        Assert.Equal(6, report.Breakdown.Count);
        Assert.Contains(report.Breakdown, b => b.Factor == "TrendAlignment");
        Assert.Contains(report.Breakdown, b => b.Factor == "Confirmations");
        Assert.Contains(report.Breakdown, b => b.Factor == "Volume");
        Assert.Contains(report.Breakdown, b => b.Factor == "RiskReward");
        Assert.Contains(report.Breakdown, b => b.Factor == "History");
        Assert.Contains(report.Breakdown, b => b.Factor == "Volatility");
    }

    [Fact]
    public void Breakdown_weights_sum_to_1()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 110m);

        var totalWeight = report.Breakdown.Sum(b => b.Weight);
        Assert.Equal(1.0m, totalWeight);
    }

    [Fact]
    public void All_breakdown_entries_have_reasons()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 110m);

        Assert.All(report.Breakdown, b => Assert.False(string.IsNullOrEmpty(b.Reason)));
    }

    // ── R:R Ratio Scoring ────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.5, 25)]
    [InlineData(0.99, 25)]
    [InlineData(1.0, 40)]
    [InlineData(1.49, 40)]
    [InlineData(1.5, 60)]
    [InlineData(1.99, 60)]
    [InlineData(2.0, 75)]
    [InlineData(2.49, 75)]
    [InlineData(2.5, 85)]
    [InlineData(2.99, 85)]
    [InlineData(3.0, 100)]
    [InlineData(5.0, 100)]
    public void RR_scoring_correct(decimal rr, decimal expectedScore)
    {
        Assert.Equal(expectedScore, ConfidenceGrader.ScoreRiskReward(rr));
    }

    // ── R:R Computation ──────────────────────────────────────

    [Fact]
    public void RR_long_computed_correctly()
    {
        // Entry 100, Stop 95 (risk=5), Target 115 (reward=15)
        var rr = ConfidenceGrader.ComputeRiskReward(SignalDirection.Long, 100m, 95m, 115m);
        Assert.Equal(3.0m, rr);
    }

    [Fact]
    public void RR_short_computed_correctly()
    {
        // Short: Entry 100, Stop 105 (risk=5), Target 85 (reward=15)
        var rr = ConfidenceGrader.ComputeRiskReward(SignalDirection.Short, 100m, 105m, 85m);
        Assert.Equal(3.0m, rr);
    }

    [Fact]
    public void RR_zero_risk_returns_zero()
    {
        // Stop at entry → zero risk
        var rr = ConfidenceGrader.ComputeRiskReward(SignalDirection.Long, 100m, 100m, 110m);
        Assert.Equal(0m, rr);
    }

    [Fact]
    public void RR_stop_wrong_side_returns_zero()
    {
        // Long but stop above entry → negative risk
        var rr = ConfidenceGrader.ComputeRiskReward(SignalDirection.Long, 100m, 105m, 110m);
        Assert.Equal(0m, rr);
    }

    // ── Trade Prices in Report ───────────────────────────────

    [Fact]
    public void Report_includes_trade_prices()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 150m, 145m, 165m);

        Assert.Equal(150m, report.EntryPrice);
        Assert.Equal(145m, report.StopPrice);
        Assert.Equal(165m, report.TargetPrice);
        Assert.Equal(3.0m, report.RiskRewardRatio);
    }

    // ── Historical Win Rate ──────────────────────────────────

    [Fact]
    public void No_history_uses_neutral_50()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 110m, historicalWinRate: null);

        var historyEntry = report.Breakdown.Single(b => b.Factor == "History");
        Assert.Equal(50m, historyEntry.RawScore);
        Assert.Contains("neutral", historyEntry.Reason);
    }

    [Fact]
    public void High_win_rate_boosts_score()
    {
        var eval = MakeEvaluation();
        var reportHigh = ConfidenceGrader.Grade(eval, 100m, 95m, 110m, historicalWinRate: 90m);
        var reportLow = ConfidenceGrader.Grade(eval, 100m, 95m, 110m, historicalWinRate: 20m);

        Assert.True(reportHigh.Score > reportLow.Score);
    }

    [Fact]
    public void Win_rate_clamped_to_0_100()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 110m, historicalWinRate: 150m);

        var historyEntry = report.Breakdown.Single(b => b.Factor == "History");
        Assert.Equal(100m, historyEntry.RawScore);
    }

    // ── Factor Impact ────────────────────────────────────────

    [Fact]
    public void Trend_alignment_has_25_percent_weight()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 110m);

        var trend = report.Breakdown.Single(b => b.Factor == "TrendAlignment");
        Assert.Equal(0.25m, trend.Weight);
    }

    [Fact]
    public void Failing_trend_significantly_lowers_score()
    {
        var evalPass = MakeEvaluation(trendPass: true);
        var evalFail = MakeEvaluation(trendPass: false);

        var reportPass = ConfidenceGrader.Grade(evalPass, 100m, 95m, 110m);
        var reportFail = ConfidenceGrader.Grade(evalFail, 100m, 95m, 110m);

        // Trend is 25% weight, 100→0 raw score = 25 point drop
        Assert.True(reportPass.Score - reportFail.Score >= 20m,
            $"Expected >= 20pt difference, got {reportPass.Score - reportFail.Score}");
    }

    // ── Short Direction ──────────────────────────────────────

    [Fact]
    public void Short_signal_grades_correctly()
    {
        var eval = MakeEvaluation(direction: SignalDirection.Short);
        // Short: entry 100, stop 105 (risk=5), target 85 (reward=15)
        var report = ConfidenceGrader.Grade(eval, 100m, 105m, 85m, historicalWinRate: 70m);

        Assert.Equal(SignalDirection.Short, report.Direction);
        Assert.Equal(3.0m, report.RiskRewardRatio);
        Assert.True(report.Score > 0);
    }

    // ── Evaluation Preserved ─────────────────────────────────

    [Fact]
    public void Report_preserves_underlying_evaluation()
    {
        var eval = MakeEvaluation();
        var report = ConfidenceGrader.Grade(eval, 100m, 95m, 110m);

        Assert.Same(eval, report.Evaluation);
        Assert.Equal(eval.Confirmations.Count, report.Evaluation.Confirmations.Count);
    }
}
