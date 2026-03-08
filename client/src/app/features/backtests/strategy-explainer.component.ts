import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-strategy-explainer',
  standalone: true,
  template: `
    <div class="collapse collapse-arrow bg-base-200 mb-2">
      <input type="checkbox" />
      <div class="collapse-title text-sm font-medium">How the Backtest Engine Works</div>
      <div class="collapse-content text-xs space-y-3 text-base-content/70">
        <p>The unified engine simulates day-by-day trading across your selected date range, applying your strategy rules to real historical price data.</p>

        <div class="divider my-1"></div>

        <h4 class="font-semibold text-base-content/90">Regime Detection</h4>
        <p>Every ~63 trading days (quarterly), the engine re-evaluates the market regime by analyzing:</p>
        <ul class="list-disc list-inside ml-2 space-y-0.5">
          <li><strong>Bull:</strong> SMA50 &gt; SMA200, positive momentum — trend-following strategies favored</li>
          <li><strong>Bear:</strong> SMA50 &lt; SMA200, negative momentum — defensive/mean-reversion strategies</li>
          <li><strong>Sideways:</strong> Low ADX, tight range — range-bound strategies</li>
          <li><strong>HighVol:</strong> ATR &gt; 2x average — reduced position sizes, wider stops</li>
        </ul>

        <div class="divider my-1"></div>

        <h4 class="font-semibold text-base-content/90">Entry Signals</h4>
        <p>Each strategy defines conditions that must be met for a buy signal. The engine evaluates all conditions and computes a composite signal score (0-100):</p>
        <ul class="list-disc list-inside ml-2 space-y-0.5">
          <li><strong>RSI Factor (0-40):</strong> Lower RSI = stronger buy signal (oversold = opportunity)</li>
          <li><strong>Volume Factor (0-30):</strong> Higher volume confirms conviction</li>
          <li><strong>ATR Factor (0-30):</strong> Moderate volatility preferred (not too calm, not too wild)</li>
        </ul>

        <div class="divider my-1"></div>

        <h4 class="font-semibold text-base-content/90">Position Sizing</h4>
        <p>Position size is determined by multiple factors working together:</p>
        <ul class="list-disc list-inside ml-2 space-y-0.5">
          <li><strong>Risk-Based:</strong> risk$ = equity x riskPercent/100</li>
          <li><strong>Signal Score Scaling:</strong> multiplier = 0.5 + (score/100) x 0.5 — stronger signals get 50-100% of base risk</li>
          <li><strong>Kelly Criterion:</strong> Optimal sizing based on win rate and payoff ratio (if enabled)</li>
          <li><strong>Volatility Targeting:</strong> Adjusts shares based on ATR to normalize risk per trade (if enabled)</li>
          <li><strong>Portfolio Heat:</strong> Total open risk capped to prevent overexposure</li>
        </ul>

        <div class="divider my-1"></div>

        <h4 class="font-semibold text-base-content/90">Exit Rules</h4>
        <ul class="list-disc list-inside ml-2 space-y-0.5">
          <li><strong>Stop Loss:</strong> Fixed percentage below entry (configurable per strategy)</li>
          <li><strong>Take Profit:</strong> Fixed percentage above entry</li>
          <li><strong>Trailing Stop:</strong> Follows price up, locks in gains</li>
          <li><strong>Time-Based:</strong> Max holding period exit</li>
          <li><strong>Condition Exit:</strong> Strategy-defined exit conditions (e.g., RSI overbought)</li>
        </ul>

        <div class="divider my-1"></div>

        <h4 class="font-semibold text-base-content/90">Risk Controls</h4>
        <ul class="list-disc list-inside ml-2 space-y-0.5">
          <li><strong>Circuit Breaker:</strong> Pauses all new entries when drawdown exceeds threshold (default 15%). Reactivates after partial recovery.</li>
          <li><strong>Max Positions:</strong> Hard cap on concurrent open positions</li>
          <li><strong>Correlation Filter:</strong> Avoids holding highly correlated positions simultaneously</li>
          <li><strong>Geographic Budget:</strong> Limits exposure per market/region</li>
        </ul>

        <div class="divider my-1"></div>

        <h4 class="font-semibold text-base-content/90">Costs & Slippage</h4>
        <ul class="list-disc list-inside ml-2 space-y-0.5">
          <li><strong>Commission:</strong> Per-trade commission (configurable)</li>
          <li><strong>Slippage:</strong> Simulated fill-price deviation on both entry and exit</li>
          <li><strong>Stamp Duty (UK):</strong> 0.5% SDRT on buy-side for UK stocks</li>
          <li><strong>FX Fee:</strong> Currency conversion cost for cross-currency trades</li>
          <li><strong>Spread:</strong> Bid-ask spread cost per trade</li>
        </ul>
      </div>
    </div>
  `,
})
export class StrategyExplainerComponent {}
