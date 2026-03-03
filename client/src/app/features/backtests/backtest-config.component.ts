import { Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../core/services/api.service';

/** Available indicators for condition building */
const INDICATORS = [
  'RSI', 'MACD', 'SMA', 'EMA', 'BollingerBands', 'Stochastic', 'ATR', 'OBV', 'WMA', 'Price', 'Volume',
] as const;

const COMPARISONS = [
  'GreaterThan', 'LessThan', 'CrossAbove', 'CrossBelow', 'Between',
] as const;

const TIMEFRAMES = ['Daily', 'Weekly', 'Monthly'] as const;

interface ConditionForm {
  indicator: string;
  comparison: string;
  value: number;
  valueHigh: number | null;
  period: number | null;
  referenceIndicator: string;
  referencePeriod: number | null;
  timeframe: string;
}

interface ParamRange {
  name: string;
  min: number;
  max: number;
  step: number;
}

interface StrategyTemplate {
  id: string;
  name: string;
  description: string;
  badge: string;
  definition: any;
  stopLoss: { type: string; multiplier: number };
  takeProfit: { type: string; multiplier: number };
  riskPercent: number;
  maxPositions: number;
  maxDrawdown: number;
}

/** Pre-built strategy templates matching backend PlaybookGenerator */
const TEMPLATES: Record<string, StrategyTemplate> = {
  Momentum: {
    id: 'Momentum',
    name: 'Momentum',
    description: 'Ride strong trends. Buys when short-term moving averages cross above long-term, with RSI confirming strength.',
    badge: 'Best for trending markets',
    definition: {
      entryConditions: [
        { timeframe: 'Daily', conditions: [
          { indicator: 'EMA', period: 20, comparison: 'CrossAbove', referenceIndicator: 'EMA', referencePeriod: 50 },
          { indicator: 'RSI', period: 14, comparison: 'GreaterThan', value: 50 },
        ]},
        { timeframe: 'Weekly', conditions: [
          { indicator: 'SMA', period: 50, comparison: 'GreaterThan', referenceIndicator: 'SMA', referencePeriod: 200 },
        ]},
      ],
      exitConditions: [
        { timeframe: 'Daily', conditions: [
          { indicator: 'EMA', period: 20, comparison: 'CrossBelow', referenceIndicator: 'EMA', referencePeriod: 50 },
          { indicator: 'RSI', period: 14, comparison: 'LessThan', value: 40 },
        ]},
      ],
    },
    stopLoss: { type: 'Atr', multiplier: 2 },
    takeProfit: { type: 'RMultiple', multiplier: 2.5 },
    riskPercent: 1,
    maxPositions: 6,
    maxDrawdown: 15,
  },
  MeanReversion: {
    id: 'MeanReversion',
    name: 'Mean Reversion',
    description: 'Buy the dip. Enters when price is oversold (low RSI, below Bollinger Band) and exits on recovery.',
    badge: 'Best for sideways markets',
    definition: {
      entryConditions: [
        { timeframe: 'Daily', conditions: [
          { indicator: 'RSI', period: 14, comparison: 'LessThan', value: 30 },
          { indicator: 'BollingerBands', period: 20, comparison: 'LessThan', value: 0 },
        ]},
        { timeframe: 'Daily', conditions: [
          { indicator: 'SMA', period: 200, comparison: 'GreaterThan', value: 0, referenceIndicator: 'Price' },
        ]},
      ],
      exitConditions: [
        { timeframe: 'Daily', conditions: [
          { indicator: 'RSI', period: 14, comparison: 'GreaterThan', value: 60 },
          { indicator: 'BollingerBands', period: 20, comparison: 'GreaterThan', value: 0 },
        ]},
      ],
    },
    stopLoss: { type: 'Atr', multiplier: 1.5 },
    takeProfit: { type: 'RMultiple', multiplier: 2 },
    riskPercent: 0.75,
    maxPositions: 5,
    maxDrawdown: 12,
  },
  Breakout: {
    id: 'Breakout',
    name: 'Breakout',
    description: 'Catch explosive moves. Enters when price breaks above Bollinger Bands with volume surge.',
    badge: 'Best for volatile markets',
    definition: {
      entryConditions: [
        { timeframe: 'Daily', conditions: [
          { indicator: 'Price', comparison: 'GreaterThan', value: 0, referenceIndicator: 'BollingerBands', referencePeriod: 20 },
          { indicator: 'Volume', comparison: 'GreaterThan', value: 1.5 },
        ]},
        { timeframe: 'Daily', conditions: [
          { indicator: 'ATR', period: 14, comparison: 'GreaterThan', value: 0 },
        ]},
      ],
      exitConditions: [
        { timeframe: 'Daily', conditions: [
          { indicator: 'Price', comparison: 'LessThan', value: 0, referenceIndicator: 'EMA', referencePeriod: 20 },
        ]},
      ],
    },
    stopLoss: { type: 'Atr', multiplier: 2.5 },
    takeProfit: { type: 'RMultiple', multiplier: 2 },
    riskPercent: 1,
    maxPositions: 5,
    maxDrawdown: 15,
  },
};

@Component({
  selector: 'app-backtest-config',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="max-w-4xl">
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold">Backtest</h2>
        <button class="btn btn-ghost btn-xs" (click)="advancedMode.set(!advancedMode())">
          {{ advancedMode() ? 'Smart Mode' : 'Advanced Mode' }}
        </button>
      </div>

      <!-- ===== SMART MODE (default) ===== -->
      @if (!advancedMode()) {

        <div class="card bg-base-200 mb-6">
          <div class="card-body p-5 gap-4">
            <p class="text-base-content/60 text-sm">Enter a stock symbol. The system will analyze its price history, detect the current market regime, and automatically pick the best strategy.</p>

            <div class="flex gap-3 items-end">
              <div class="form-control flex-1">
                <label class="label"><span class="label-text text-xs">Symbol</span></label>
                <input class="input input-bordered input-lg font-mono uppercase text-center tracking-wider"
                       [(ngModel)]="symbol" placeholder="AAPL"
                       (keydown.enter)="runSmartBacktest()" />
              </div>
              <div class="form-control">
                <label class="label"><span class="label-text text-xs">Start</span></label>
                <input type="date" class="input input-bordered input-sm" [(ngModel)]="startDate" />
              </div>
              <div class="form-control">
                <label class="label"><span class="label-text text-xs">End</span></label>
                <input type="date" class="input input-bordered input-sm" [(ngModel)]="endDate" />
              </div>
            </div>
          </div>
        </div>

        <!-- Regime detection result (shown after analysis) -->
        @if (detectedRegime()) {
          <div class="card bg-base-200 mb-6">
            <div class="card-body p-4 gap-3">
              <div class="flex items-center gap-3">
                <div class="badge badge-lg"
                     [class.badge-success]="detectedRegime()!.regime === 'Bull'"
                     [class.badge-error]="detectedRegime()!.regime === 'Bear'"
                     [class.badge-warning]="detectedRegime()!.regime === 'HighVolatility'"
                     [class.badge-info]="detectedRegime()!.regime === 'Sideways'">
                  {{ detectedRegime()!.regime }}
                </div>
                <span class="text-sm font-semibold">{{ detectedRegime()!.recommendedTemplate }} Strategy Selected</span>
                <span class="text-xs text-base-content/40">Confidence: {{ detectedRegime()!.confidence | number:'1.0-0' }}%</span>
              </div>
              <p class="text-xs text-base-content/60">{{ detectedRegime()!.explanation }}</p>

              @if (getTemplate(detectedRegime()!.recommendedTemplate); as tpl) {
                <div class="grid grid-cols-3 gap-2 text-xs font-mono mt-1">
                  <span>Stop Loss: {{ tpl.stopLoss.type }} x{{ tpl.stopLoss.multiplier }}</span>
                  <span>Take Profit: {{ tpl.takeProfit.type }} x{{ tpl.takeProfit.multiplier }}</span>
                  <span>Risk/Trade: {{ tpl.riskPercent }}%</span>
                </div>
              }
            </div>
          </div>
        }

        <!-- Error -->
        @if (error()) {
          <div class="alert alert-error text-sm mb-4">{{ error() }}</div>
        }

        <!-- Progress -->
        @if (running()) {
          <div class="flex items-center gap-3 mb-4">
            <span class="loading loading-spinner loading-md"></span>
            <span class="text-sm">{{ runningLabel() }}</span>
          </div>
        }

        <!-- Run button -->
        <button class="btn btn-primary btn-lg w-full"
                (click)="runSmartBacktest()"
                [disabled]="running() || !symbol.trim()">
          @if (!symbol.trim()) {
            Enter a symbol above
          } @else {
            Analyze & Backtest {{ symbol.trim().toUpperCase() }}
          }
        </button>
      }

      <!-- ===== ADVANCED MODE ===== -->
      @if (advancedMode()) {

        <ul class="steps steps-horizontal w-full mb-8 text-xs">
          <li class="step" [class.step-primary]="step() >= 1">Strategy</li>
          <li class="step" [class.step-primary]="step() >= 2">Parameters</li>
          <li class="step" [class.step-primary]="step() >= 3">Risk Settings</li>
          <li class="step" [class.step-primary]="step() >= 4">Review & Run</li>
        </ul>

        @if (step() === 1) {
          <div class="card bg-base-200">
            <div class="card-body gap-4">
              <h3 class="card-title text-lg">Strategy Definition</h3>

              <div class="form-control">
                <label class="label"><span class="label-text">Strategy</span></label>
                <div class="flex gap-2">
                  <select class="select select-bordered select-sm flex-1" [(ngModel)]="selectedStrategyId">
                    <option value="">-- Create new --</option>
                    @for (s of strategies(); track s.id) {
                      <option [value]="s.id">{{ s.name }}</option>
                    }
                  </select>
                  <button class="btn btn-ghost btn-sm" (click)="loadStrategies()">Refresh</button>
                </div>
              </div>

              @if (!selectedStrategyId) {
                <div class="flex gap-2 items-center">
                  <span class="text-xs text-base-content/60">Quick fill:</span>
                  @for (tpl of templateList; track tpl.id) {
                    <button class="btn btn-xs btn-outline" (click)="applyTemplate(tpl.id)">{{ tpl.name }}</button>
                  }
                </div>

                <div class="divider text-xs">New Strategy</div>

                <div class="grid grid-cols-2 gap-3">
                  <div class="form-control">
                    <label class="label"><span class="label-text text-xs">Name</span></label>
                    <input class="input input-bordered input-sm" [(ngModel)]="strategyName" placeholder="My Strategy" />
                  </div>
                  <div class="form-control">
                    <label class="label"><span class="label-text text-xs">Description</span></label>
                    <input class="input input-bordered input-sm" [(ngModel)]="strategyDescription" placeholder="Optional" />
                  </div>
                </div>

                <div class="mt-2">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-sm font-semibold">Entry Conditions</span>
                    <button class="btn btn-xs btn-primary" (click)="addCondition('entry')">+ Add</button>
                  </div>
                  @for (cond of entryConditions; track $index) {
                    <div class="flex gap-2 items-end mb-2 flex-wrap">
                      <select class="select select-bordered select-xs w-24" [(ngModel)]="cond.timeframe">
                        @for (tf of timeframes; track tf) { <option [value]="tf">{{ tf }}</option> }
                      </select>
                      <select class="select select-bordered select-xs w-28" [(ngModel)]="cond.indicator">
                        @for (ind of indicators; track ind) { <option [value]="ind">{{ ind }}</option> }
                      </select>
                      <input type="number" class="input input-bordered input-xs w-16" [(ngModel)]="cond.period" placeholder="Period" />
                      <select class="select select-bordered select-xs w-28" [(ngModel)]="cond.comparison">
                        @for (cmp of comparisons; track cmp) { <option [value]="cmp">{{ cmp }}</option> }
                      </select>
                      <input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="cond.value" placeholder="Value" />
                      @if (cond.comparison === 'CrossAbove' || cond.comparison === 'CrossBelow') {
                        <select class="select select-bordered select-xs w-24" [(ngModel)]="cond.referenceIndicator">
                          <option value="">Value</option>
                          @for (ind of indicators; track ind) { <option [value]="ind">{{ ind }}</option> }
                        </select>
                        @if (cond.referenceIndicator) {
                          <input type="number" class="input input-bordered input-xs w-16" [(ngModel)]="cond.referencePeriod" placeholder="Per" />
                        }
                      }
                      <button class="btn btn-xs btn-ghost text-error" (click)="removeCondition('entry', $index)">x</button>
                    </div>
                  }
                </div>

                <div class="mt-2">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-sm font-semibold">Exit Conditions</span>
                    <button class="btn btn-xs btn-outline" (click)="addCondition('exit')">+ Add</button>
                  </div>
                  @for (cond of exitConditions; track $index) {
                    <div class="flex gap-2 items-end mb-2 flex-wrap">
                      <select class="select select-bordered select-xs w-24" [(ngModel)]="cond.timeframe">
                        @for (tf of timeframes; track tf) { <option [value]="tf">{{ tf }}</option> }
                      </select>
                      <select class="select select-bordered select-xs w-28" [(ngModel)]="cond.indicator">
                        @for (ind of indicators; track ind) { <option [value]="ind">{{ ind }}</option> }
                      </select>
                      <input type="number" class="input input-bordered input-xs w-16" [(ngModel)]="cond.period" placeholder="Period" />
                      <select class="select select-bordered select-xs w-28" [(ngModel)]="cond.comparison">
                        @for (cmp of comparisons; track cmp) { <option [value]="cmp">{{ cmp }}</option> }
                      </select>
                      <input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="cond.value" placeholder="Value" />
                      <button class="btn btn-xs btn-ghost text-error" (click)="removeCondition('exit', $index)">x</button>
                    </div>
                  }
                </div>

                <div class="grid grid-cols-2 gap-4 mt-2">
                  <div>
                    <span class="text-sm font-semibold">Stop Loss</span>
                    <div class="flex gap-2 mt-1">
                      <select class="select select-bordered select-xs flex-1" [(ngModel)]="stopLossType">
                        <option value="Atr">ATR</option>
                        <option value="FixedPercent">Fixed %</option>
                      </select>
                      <input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="stopLossMultiplier" step="0.1" />
                    </div>
                  </div>
                  <div>
                    <span class="text-sm font-semibold">Take Profit</span>
                    <div class="flex gap-2 mt-1">
                      <select class="select select-bordered select-xs flex-1" [(ngModel)]="takeProfitType">
                        <option value="RMultiple">R Multiple</option>
                        <option value="FixedPercent">Fixed %</option>
                      </select>
                      <input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="takeProfitMultiplier" step="0.1" />
                    </div>
                  </div>
                </div>
              }

              <div class="card-actions justify-end mt-4">
                <button class="btn btn-primary btn-sm" (click)="step.set(2)">Next: Parameters</button>
              </div>
            </div>
          </div>
        }

        @if (step() === 2) {
          <div class="card bg-base-200">
            <div class="card-body gap-4">
              <h3 class="card-title text-lg">Backtest Parameters</h3>
              <div class="grid grid-cols-3 gap-3">
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Symbol</span></label>
                  <input class="input input-bordered input-sm font-mono uppercase" [(ngModel)]="symbol" placeholder="AAPL" />
                </div>
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Start Date</span></label>
                  <input type="date" class="input input-bordered input-sm" [(ngModel)]="startDate" />
                </div>
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">End Date</span></label>
                  <input type="date" class="input input-bordered input-sm" [(ngModel)]="endDate" />
                </div>
              </div>
              <div class="divider text-xs">Optimization Ranges (for Walk-Forward)</div>
              <div class="flex items-center justify-between mb-1">
                <span class="text-sm font-semibold">Parameter Ranges</span>
                <button class="btn btn-xs btn-outline" (click)="addParamRange()">+ Add</button>
              </div>
              @for (p of paramRanges; track $index) {
                <div class="flex gap-2 items-end mb-2">
                  <input class="input input-bordered input-xs w-32" [(ngModel)]="p.name" placeholder="Param name" />
                  <div class="flex flex-col"><span class="text-[10px] text-base-content/40">Min</span><input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="p.min" /></div>
                  <div class="flex flex-col"><span class="text-[10px] text-base-content/40">Max</span><input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="p.max" /></div>
                  <div class="flex flex-col"><span class="text-[10px] text-base-content/40">Step</span><input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="p.step" /></div>
                  <button class="btn btn-xs btn-ghost text-error" (click)="paramRanges.splice($index, 1)">x</button>
                </div>
              }
              @if (paramRanges.length > 0) {
                <div class="text-xs text-base-content/40">
                  Total combinations: {{ totalCombinations() | number }}
                  @if (totalCombinations() > 10000) { <span class="text-warning ml-2">Large search space</span> }
                </div>
              }
              <div class="card-actions justify-between mt-4">
                <button class="btn btn-ghost btn-sm" (click)="step.set(1)">Back</button>
                <button class="btn btn-primary btn-sm" (click)="step.set(3)">Next: Risk Settings</button>
              </div>
            </div>
          </div>
        }

        @if (step() === 3) {
          <div class="card bg-base-200">
            <div class="card-body gap-4">
              <h3 class="card-title text-lg">Risk & Position Sizing</h3>
              <div class="grid grid-cols-2 gap-4">
                <div class="form-control"><label class="label"><span class="label-text text-xs">Risk per Trade (%)</span></label><input type="number" class="input input-bordered input-sm" [(ngModel)]="riskPercent" step="0.5" min="0.1" max="10" /></div>
                <div class="form-control"><label class="label"><span class="label-text text-xs">Max Open Positions</span></label><input type="number" class="input input-bordered input-sm" [(ngModel)]="maxPositions" min="1" max="20" /></div>
                <div class="form-control"><label class="label"><span class="label-text text-xs">Max Portfolio Heat (%)</span></label><input type="number" class="input input-bordered input-sm" [(ngModel)]="maxPortfolioHeat" step="0.5" /></div>
                <div class="form-control"><label class="label"><span class="label-text text-xs">Max Drawdown (%)</span></label><input type="number" class="input input-bordered input-sm" [(ngModel)]="maxDrawdown" step="1" /></div>
                <div class="form-control"><label class="label"><span class="label-text text-xs">Drawdown Recovery (%)</span></label><input type="number" class="input input-bordered input-sm" [(ngModel)]="drawdownRecovery" step="1" /></div>
                <div class="form-control"><label class="label"><span class="label-text text-xs">Initial Capital ($)</span></label><input type="number" class="input input-bordered input-sm" [(ngModel)]="initialCapital" step="1000" /></div>
              </div>
              <div class="card-actions justify-between mt-4">
                <button class="btn btn-ghost btn-sm" (click)="step.set(2)">Back</button>
                <button class="btn btn-primary btn-sm" (click)="step.set(4)">Next: Review</button>
              </div>
            </div>
          </div>
        }

        @if (step() === 4) {
          <div class="card bg-base-200">
            <div class="card-body gap-4">
              <h3 class="card-title text-lg">Review & Run</h3>
              <div class="overflow-x-auto">
                <table class="table table-xs">
                  <tbody>
                    <tr><td class="text-base-content/60 w-40">Strategy</td><td class="font-mono">{{ selectedStrategyId || strategyName || '(new)' }}</td></tr>
                    <tr><td class="text-base-content/60">Symbol</td><td class="font-mono uppercase">{{ symbol }}</td></tr>
                    <tr><td class="text-base-content/60">Date Range</td><td class="font-mono">{{ startDate }} to {{ endDate }}</td></tr>
                    <tr><td class="text-base-content/60">Entry Conditions</td><td>{{ selectedStrategyId ? '(from strategy)' : entryConditions.length + ' condition(s)' }}</td></tr>
                    <tr><td class="text-base-content/60">Stop Loss</td><td class="font-mono">{{ stopLossType }} x{{ stopLossMultiplier }}</td></tr>
                    <tr><td class="text-base-content/60">Risk / Trade</td><td class="font-mono">{{ riskPercent }}%</td></tr>
                    <tr><td class="text-base-content/60">Max Drawdown</td><td class="font-mono">{{ maxDrawdown }}%</td></tr>
                  </tbody>
                </table>
              </div>
              @if (!isNewStrategyValid()) {
                <div class="alert alert-warning text-sm">Add at least one entry condition on Step 1 before running.</div>
              }
              @if (error()) { <div class="alert alert-error text-sm">{{ error() }}</div> }
              @if (running()) {
                <div class="flex items-center gap-3"><span class="loading loading-spinner loading-md"></span><span class="text-sm">{{ runningLabel() }}</span></div>
              }
              <div class="card-actions justify-between mt-4">
                <button class="btn btn-ghost btn-sm" (click)="step.set(3)" [disabled]="running()">Back</button>
                <button class="btn btn-primary btn-sm" (click)="runBacktest()" [disabled]="running() || !isNewStrategyValid()">Run Backtest</button>
              </div>
            </div>
          </div>
        }
      }
    </div>
  `,
})
export class BacktestConfigComponent {
  private api = inject(ApiService);
  private router = inject(Router);

  // Mode toggle
  advancedMode = signal(false);

  // Smart mode state
  detectedRegime = signal<any>(null);

  // Step control (advanced mode)
  step = signal(1);

  // Strategy selection (advanced mode)
  strategies = signal<any[]>([]);
  selectedStrategyId = '';

  // New strategy fields
  strategyName = '';
  strategyDescription = '';
  entryConditions: ConditionForm[] = [];
  exitConditions: ConditionForm[] = [];
  stopLossType = 'Atr';
  stopLossMultiplier = 2;
  takeProfitType = 'RMultiple';
  takeProfitMultiplier = 2;

  // Backtest params
  symbol = 'AAPL';
  startDate = new Date(Date.now() - 2 * 365.25 * 86400000).toISOString().slice(0, 10); // 2 years ago
  endDate = new Date().toISOString().slice(0, 10); // today

  // Optimization param ranges
  paramRanges: ParamRange[] = [];

  // Risk settings
  riskPercent = 1;
  maxPositions = 6;
  maxPortfolioHeat = 6;
  maxDrawdown = 15;
  drawdownRecovery = 5;
  initialCapital = 100000;

  // Run state
  running = signal(false);
  runningLabel = signal('');
  error = signal('');

  // Constants
  indicators = INDICATORS;
  comparisons = COMPARISONS;
  timeframes = TIMEFRAMES;
  templateList = Object.values(TEMPLATES);

  constructor() {
    this.loadStrategies();
  }

  getTemplate(id: string): StrategyTemplate | undefined {
    return TEMPLATES[id];
  }

  // ===== SMART MODE =====

  async runSmartBacktest(): Promise<void> {
    const sym = this.symbol.trim().toUpperCase();
    if (!sym) return;

    this.running.set(true);
    this.error.set('');
    this.detectedRegime.set(null);
    this.runningLabel.set(`Analyzing ${sym}...`);

    try {
      // Step 1: Detect regime (also tells us if data is missing)
      let regime = await this.detectRegime(sym);

      // Step 2: If no/insufficient data, auto-fetch from Yahoo Finance
      if (regime.regime === 'Unknown') {
        this.runningLabel.set(`Fetching ${sym} market data from Yahoo Finance (this may take a moment)...`);
        await this.ingestData(sym);
        // Re-detect with real data now available
        this.runningLabel.set(`Analyzing ${sym} market regime...`);
        regime = await this.detectRegime(sym);
      }

      this.detectedRegime.set(regime);

      // Step 3: Get the matching template
      const tpl = TEMPLATES[regime.recommendedTemplate] ?? TEMPLATES['Momentum'];
      this.runningLabel.set(`Creating ${tpl.name} strategy for ${sym}...`);

      // Step 4: Create the strategy
      const strategyId = await this.createTemplateStrategy(tpl, sym);

      // Step 5: Run the backtest
      this.runningLabel.set(`Running ${tpl.name} backtest on ${sym}...`);
      this.api.runBacktest({
        strategyId,
        symbol: sym,
        startDate: this.startDate,
        endDate: this.endDate,
      }).subscribe({
        next: (result: any) => {
          this.running.set(false);
          this.router.navigate(['/backtests', result.id]);
        },
        error: (err: any) => {
          this.running.set(false);
          this.error.set(err.error?.detail ?? err.message ?? 'Backtest failed');
        },
      });
    } catch (e: any) {
      this.running.set(false);
      this.error.set(e.error?.detail ?? e.message ?? 'Something went wrong. Check the symbol and try again.');
    }
  }

  private detectRegime(symbol: string): Promise<any> {
    return new Promise((resolve, reject) => {
      this.api.detectStockRegime(symbol).subscribe({
        next: (res: any) => {
          if (res.confidence <= 1) res.confidence = res.confidence * 100;
          resolve(res);
        },
        error: (err: any) => reject(err),
      });
    });
  }

  private ingestData(symbol: string): Promise<void> {
    return new Promise((resolve, reject) => {
      this.api.ingestMarketData(symbol, 3).subscribe({
        next: () => resolve(),
        error: (err: any) => reject(err),
      });
    });
  }

  private createTemplateStrategy(tpl: StrategyTemplate, sym: string): Promise<string> {
    return new Promise((resolve, reject) => {
      const definition = {
        ...tpl.definition,
        stopLoss: tpl.stopLoss,
        takeProfit: tpl.takeProfit,
        positionSizing: {
          riskPercent: tpl.riskPercent,
          maxPositions: tpl.maxPositions,
          maxPortfolioHeat: this.maxPortfolioHeat,
          maxDrawdownPercent: tpl.maxDrawdown,
          drawdownRecoveryPercent: this.drawdownRecovery,
        },
        filters: {},
      };

      this.api.createStrategyV2({
        name: `${tpl.name} — ${sym} ${new Date().toISOString().slice(0, 10)}`,
        description: `Auto-selected ${tpl.name} strategy for ${sym}`,
        definition,
      }).subscribe({
        next: (res: any) => resolve(res.id),
        error: (err: any) => reject(err),
      });
    });
  }

  // ===== ADVANCED MODE =====

  loadStrategies(): void {
    this.api.getStrategies({ page: 1, pageSize: 100 }).subscribe({
      next: (res: any) => this.strategies.set(res.items ?? []),
      error: () => this.strategies.set([]),
    });
  }

  applyTemplate(templateId: string): void {
    const tpl = TEMPLATES[templateId];
    if (!tpl) return;
    this.strategyName = `${tpl.name} Strategy`;
    this.stopLossType = tpl.stopLoss.type;
    this.stopLossMultiplier = tpl.stopLoss.multiplier;
    this.takeProfitType = tpl.takeProfit.type;
    this.takeProfitMultiplier = tpl.takeProfit.multiplier;
    this.riskPercent = tpl.riskPercent;
    this.maxPositions = tpl.maxPositions;
    this.maxDrawdown = tpl.maxDrawdown;
    this.entryConditions = this.flattenConditions(tpl.definition.entryConditions);
    this.exitConditions = this.flattenConditions(tpl.definition.exitConditions);
  }

  addCondition(type: 'entry' | 'exit'): void {
    const cond: ConditionForm = { indicator: 'RSI', comparison: 'LessThan', value: 30, valueHigh: null, period: 14, referenceIndicator: '', referencePeriod: null, timeframe: 'Daily' };
    if (type === 'entry') this.entryConditions.push(cond);
    else this.exitConditions.push(cond);
  }

  removeCondition(type: 'entry' | 'exit', index: number): void {
    if (type === 'entry') this.entryConditions.splice(index, 1);
    else this.exitConditions.splice(index, 1);
  }

  addParamRange(): void { this.paramRanges.push({ name: '', min: 10, max: 50, step: 10 }); }

  totalCombinations(): number {
    if (this.paramRanges.length === 0) return 0;
    return this.paramRanges.reduce((acc, p) => { const c = p.step > 0 ? Math.floor((p.max - p.min) / p.step) + 1 : 1; return acc * Math.max(c, 1); }, 1);
  }

  isNewStrategyValid(): boolean {
    if (this.selectedStrategyId) return true;
    return this.entryConditions.length > 0;
  }

  async runBacktest(): Promise<void> {
    this.running.set(true);
    this.runningLabel.set('Running backtest...');
    this.error.set('');
    try {
      const strategyId = await this.ensureStrategy();
      if (!strategyId) return;
      this.api.runBacktest({ strategyId, symbol: this.symbol.toUpperCase(), startDate: this.startDate, endDate: this.endDate }).subscribe({
        next: (result: any) => { this.running.set(false); this.router.navigate(['/backtests', result.id]); },
        error: (err: any) => { this.running.set(false); this.error.set(err.error?.detail ?? err.message ?? 'Backtest failed'); },
      });
    } catch (e: any) { this.running.set(false); this.error.set(e.message ?? 'Failed to create strategy'); }
  }

  private ensureStrategy(): Promise<string> {
    return new Promise((resolve, reject) => {
      if (this.selectedStrategyId) { resolve(this.selectedStrategyId); return; }
      if (this.entryConditions.length === 0) { reject(new Error('Add at least one entry condition.')); return; }
      const definition = {
        entryConditions: this.buildConditionGroups(this.entryConditions),
        exitConditions: this.buildConditionGroups(this.exitConditions),
        stopLoss: { type: this.stopLossType, multiplier: this.stopLossMultiplier },
        takeProfit: { type: this.takeProfitType, multiplier: this.takeProfitMultiplier },
        positionSizing: { riskPercent: this.riskPercent, maxPositions: this.maxPositions, maxPortfolioHeat: this.maxPortfolioHeat, maxDrawdownPercent: this.maxDrawdown, drawdownRecoveryPercent: this.drawdownRecovery },
        filters: {},
      };
      this.api.createStrategyV2({
        name: this.strategyName || `Strategy ${new Date().toISOString().slice(0, 10)}`,
        description: this.strategyDescription || null,
        definition,
      }).subscribe({ next: (res: any) => { this.selectedStrategyId = res.id; resolve(res.id); }, error: (err: any) => reject(err) });
    });
  }

  private buildConditionGroups(conditions: ConditionForm[]): any[] {
    const groups = new Map<string, any[]>();
    for (const c of conditions) {
      if (!groups.has(c.timeframe)) groups.set(c.timeframe, []);
      const cond: any = { indicator: c.indicator, comparison: c.comparison, value: c.value };
      if (c.period) cond.period = c.period;
      if (c.valueHigh) cond.valueHigh = c.valueHigh;
      if (c.referenceIndicator) { cond.referenceIndicator = c.referenceIndicator; if (c.referencePeriod) cond.referencePeriod = c.referencePeriod; }
      groups.get(c.timeframe)!.push(cond);
    }
    return Array.from(groups.entries()).map(([timeframe, conds]) => ({ timeframe, conditions: conds }));
  }

  private flattenConditions(groups: any[]): ConditionForm[] {
    const result: ConditionForm[] = [];
    for (const group of groups) {
      for (const c of group.conditions) {
        result.push({ indicator: c.indicator, comparison: c.comparison, value: c.value ?? 0, valueHigh: null, period: c.period ?? null, referenceIndicator: c.referenceIndicator ?? '', referencePeriod: c.referencePeriod ?? null, timeframe: group.timeframe });
      }
    }
    return result;
  }
}
