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
const TEMPLATES: StrategyTemplate[] = [
  {
    id: 'momentum',
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
  {
    id: 'meanReversion',
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
  {
    id: 'breakout',
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
];

@Component({
  selector: 'app-backtest-config',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="max-w-4xl">
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold">Backtest</h2>
        <button class="btn btn-ghost btn-xs" (click)="advancedMode.set(!advancedMode())">
          {{ advancedMode() ? 'Simple Mode' : 'Advanced Mode' }}
        </button>
      </div>

      <!-- ===== QUICK START MODE (default) ===== -->
      @if (!advancedMode()) {

        <!-- Strategy template cards -->
        <div class="mb-6">
          <p class="text-sm text-base-content/60 mb-3">Pick a strategy style:</p>
          <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
            @for (tpl of templates; track tpl.id) {
              <div class="card bg-base-200 cursor-pointer transition-all hover:shadow-lg"
                   [class.ring-2]="selectedTemplate() === tpl.id"
                   [class.ring-primary]="selectedTemplate() === tpl.id"
                   (click)="selectTemplate(tpl.id)">
                <div class="card-body p-4 gap-2">
                  <div class="flex items-center gap-2">
                    <h3 class="card-title text-sm">{{ tpl.name }}</h3>
                    @if (selectedTemplate() === tpl.id) {
                      <span class="badge badge-primary badge-xs">Selected</span>
                    }
                  </div>
                  <p class="text-xs text-base-content/60">{{ tpl.description }}</p>
                  <span class="badge badge-outline badge-xs mt-1">{{ tpl.badge }}</span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Symbol + date range -->
        <div class="card bg-base-200 mb-6">
          <div class="card-body p-4 gap-3">
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
          </div>
        </div>

        <!-- Quick summary of what will run -->
        @if (selectedTemplate()) {
          <div class="card bg-base-200 mb-6">
            <div class="card-body p-4 gap-1">
              <p class="text-xs text-base-content/60">Strategy summary:</p>
              @if (getSelectedTemplate(); as tpl) {
                <div class="grid grid-cols-3 gap-2 text-xs font-mono">
                  <span>Stop Loss: {{ tpl.stopLoss.type }} x{{ tpl.stopLoss.multiplier }}</span>
                  <span>Take Profit: {{ tpl.takeProfit.type }} x{{ tpl.takeProfit.multiplier }}</span>
                  <span>Risk/Trade: {{ tpl.riskPercent }}%</span>
                  <span>Max Positions: {{ tpl.maxPositions }}</span>
                  <span>Max Drawdown: {{ tpl.maxDrawdown }}%</span>
                  <span>Conditions: {{ tpl.definition.entryConditions.length }} entry group(s)</span>
                </div>
              }
            </div>
          </div>
        }

        <!-- Run button -->
        @if (error()) {
          <div class="alert alert-error text-sm mb-4">{{ error() }}</div>
        }
        @if (running()) {
          <div class="flex items-center gap-3 mb-4">
            <span class="loading loading-spinner loading-md"></span>
            <span class="text-sm">{{ runningLabel() }}</span>
          </div>
        }
        <button class="btn btn-primary w-full"
                (click)="runQuickBacktest()"
                [disabled]="running() || !selectedTemplate() || !symbol.trim()">
          @if (!selectedTemplate()) {
            Select a strategy above
          } @else if (!symbol.trim()) {
            Enter a symbol
          } @else {
            Run Backtest on {{ symbol.toUpperCase() }}
          }
        </button>
      }

      <!-- ===== ADVANCED MODE (original 4-step wizard) ===== -->
      @if (advancedMode()) {

        <!-- Step indicator -->
        <ul class="steps steps-horizontal w-full mb-8 text-xs">
          <li class="step" [class.step-primary]="step() >= 1">Strategy</li>
          <li class="step" [class.step-primary]="step() >= 2">Parameters</li>
          <li class="step" [class.step-primary]="step() >= 3">Risk Settings</li>
          <li class="step" [class.step-primary]="step() >= 4">Review & Run</li>
        </ul>

        <!-- Step 1: Strategy Definition -->
        @if (step() === 1) {
          <div class="card bg-base-200">
            <div class="card-body gap-4">
              <h3 class="card-title text-lg">Strategy Definition</h3>

              <!-- Use existing or create new -->
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
                <!-- Template quick-fill buttons -->
                <div class="flex gap-2 items-center">
                  <span class="text-xs text-base-content/60">Quick fill:</span>
                  @for (tpl of templates; track tpl.id) {
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

                <!-- Entry conditions -->
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

                <!-- Exit conditions -->
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

                <!-- Stop Loss / Take Profit -->
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

        <!-- Step 2: Symbol & Date Range + Optimization Params -->
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

              <!-- Optimization parameter ranges -->
              <div class="divider text-xs">Optimization Ranges (for Walk-Forward)</div>
              <div class="flex items-center justify-between mb-1">
                <span class="text-sm font-semibold">Parameter Ranges</span>
                <button class="btn btn-xs btn-outline" (click)="addParamRange()">+ Add</button>
              </div>

              @for (p of paramRanges; track $index) {
                <div class="flex gap-2 items-end mb-2">
                  <input class="input input-bordered input-xs w-32" [(ngModel)]="p.name" placeholder="Param name" />
                  <div class="flex flex-col">
                    <span class="text-[10px] text-base-content/40">Min</span>
                    <input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="p.min" />
                  </div>
                  <div class="flex flex-col">
                    <span class="text-[10px] text-base-content/40">Max</span>
                    <input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="p.max" />
                  </div>
                  <div class="flex flex-col">
                    <span class="text-[10px] text-base-content/40">Step</span>
                    <input type="number" class="input input-bordered input-xs w-20" [(ngModel)]="p.step" />
                  </div>
                  <button class="btn btn-xs btn-ghost text-error" (click)="paramRanges.splice($index, 1)">x</button>
                </div>
              }

              @if (paramRanges.length > 0) {
                <div class="text-xs text-base-content/40">
                  Total combinations: {{ totalCombinations() | number }}
                  @if (totalCombinations() > 10000) {
                    <span class="text-warning ml-2">Large search space — may be slow</span>
                  }
                </div>
              }

              <div class="card-actions justify-between mt-4">
                <button class="btn btn-ghost btn-sm" (click)="step.set(1)">Back</button>
                <button class="btn btn-primary btn-sm" (click)="step.set(3)">Next: Risk Settings</button>
              </div>
            </div>
          </div>
        }

        <!-- Step 3: Capital Preservation / Risk Settings -->
        @if (step() === 3) {
          <div class="card bg-base-200">
            <div class="card-body gap-4">
              <h3 class="card-title text-lg">Risk & Position Sizing</h3>

              <div class="grid grid-cols-2 gap-4">
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Risk per Trade (%)</span></label>
                  <input type="number" class="input input-bordered input-sm" [(ngModel)]="riskPercent" step="0.5" min="0.1" max="10" />
                </div>
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Max Open Positions</span></label>
                  <input type="number" class="input input-bordered input-sm" [(ngModel)]="maxPositions" min="1" max="20" />
                </div>
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Max Portfolio Heat (%)</span></label>
                  <input type="number" class="input input-bordered input-sm" [(ngModel)]="maxPortfolioHeat" step="0.5" />
                </div>
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Max Drawdown Circuit Breaker (%)</span></label>
                  <input type="number" class="input input-bordered input-sm" [(ngModel)]="maxDrawdown" step="1" />
                </div>
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Drawdown Recovery (%)</span></label>
                  <input type="number" class="input input-bordered input-sm" [(ngModel)]="drawdownRecovery" step="1" />
                </div>
                <div class="form-control">
                  <label class="label"><span class="label-text text-xs">Initial Capital ($)</span></label>
                  <input type="number" class="input input-bordered input-sm" [(ngModel)]="initialCapital" step="1000" />
                </div>
              </div>

              <div class="card-actions justify-between mt-4">
                <button class="btn btn-ghost btn-sm" (click)="step.set(2)">Back</button>
                <button class="btn btn-primary btn-sm" (click)="step.set(4)">Next: Review</button>
              </div>
            </div>
          </div>
        }

        <!-- Step 4: Review & Run -->
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
                    <tr><td class="text-base-content/60">Take Profit</td><td class="font-mono">{{ takeProfitType }} x{{ takeProfitMultiplier }}</td></tr>
                    <tr><td class="text-base-content/60">Risk / Trade</td><td class="font-mono">{{ riskPercent }}%</td></tr>
                    <tr><td class="text-base-content/60">Max Positions</td><td class="font-mono">{{ maxPositions }}</td></tr>
                    <tr><td class="text-base-content/60">Max Drawdown</td><td class="font-mono">{{ maxDrawdown }}%</td></tr>
                    @if (paramRanges.length > 0) {
                      <tr><td class="text-base-content/60">Optimization</td><td class="font-mono">{{ paramRanges.length }} params, {{ totalCombinations() | number }} combos</td></tr>
                    }
                  </tbody>
                </table>
              </div>

              @if (!isNewStrategyValid()) {
                <div class="alert alert-warning text-sm">Add at least one entry condition on Step 1 before running.</div>
              }
              @if (error()) {
                <div class="alert alert-error text-sm">{{ error() }}</div>
              }
              @if (running()) {
                <div class="flex items-center gap-3">
                  <span class="loading loading-spinner loading-md"></span>
                  <span class="text-sm">{{ runningLabel() }}</span>
                </div>
              }

              <div class="card-actions justify-between mt-4">
                <button class="btn btn-ghost btn-sm" (click)="step.set(3)" [disabled]="running()">Back</button>
                <div class="flex gap-2">
                  <button class="btn btn-primary btn-sm" (click)="runBacktest()" [disabled]="running() || !isNewStrategyValid()">
                    Run Backtest
                  </button>
                  @if (paramRanges.length > 0) {
                    <button class="btn btn-secondary btn-sm" (click)="runWalkForward()" [disabled]="running() || !isNewStrategyValid()">
                      Run Walk-Forward
                    </button>
                  }
                </div>
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

  // Quick-start template selection
  templates = TEMPLATES;
  selectedTemplate = signal('');

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
  startDate = '2023-01-01';
  endDate = '2025-12-31';

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

  // Constants for template
  indicators = INDICATORS;
  comparisons = COMPARISONS;
  timeframes = TIMEFRAMES;

  constructor() {
    this.loadStrategies();
  }

  loadStrategies(): void {
    this.api.getStrategies({ page: 1, pageSize: 100 }).subscribe({
      next: (res: any) => this.strategies.set(res.items ?? []),
      error: () => this.strategies.set([]),
    });
  }

  selectTemplate(id: string): void {
    this.selectedTemplate.set(this.selectedTemplate() === id ? '' : id);
  }

  getSelectedTemplate(): StrategyTemplate | undefined {
    return this.templates.find(t => t.id === this.selectedTemplate());
  }

  /** Apply a template to the advanced-mode form fields */
  applyTemplate(templateId: string): void {
    const tpl = this.templates.find(t => t.id === templateId);
    if (!tpl) return;

    this.strategyName = `${tpl.name} Strategy`;
    this.stopLossType = tpl.stopLoss.type;
    this.stopLossMultiplier = tpl.stopLoss.multiplier;
    this.takeProfitType = tpl.takeProfit.type;
    this.takeProfitMultiplier = tpl.takeProfit.multiplier;
    this.riskPercent = tpl.riskPercent;
    this.maxPositions = tpl.maxPositions;
    this.maxDrawdown = tpl.maxDrawdown;

    // Flatten template definition conditions into ConditionForm arrays
    this.entryConditions = this.flattenConditions(tpl.definition.entryConditions);
    this.exitConditions = this.flattenConditions(tpl.definition.exitConditions);
  }

  /** Quick-start: create strategy from template and immediately run backtest */
  async runQuickBacktest(): Promise<void> {
    const tpl = this.getSelectedTemplate();
    if (!tpl || !this.symbol.trim()) return;

    this.running.set(true);
    this.runningLabel.set(`Creating ${tpl.name} strategy and running backtest...`);
    this.error.set('');

    try {
      const strategyId = await this.createTemplateStrategy(tpl);

      this.api.runBacktest({
        strategyId,
        symbol: this.symbol.trim().toUpperCase(),
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
      this.error.set(e.error?.detail ?? e.message ?? 'Failed to create strategy');
    }
  }

  private createTemplateStrategy(tpl: StrategyTemplate): Promise<string> {
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
        name: `${tpl.name} — ${this.symbol.trim().toUpperCase()} ${new Date().toISOString().slice(0, 10)}`,
        description: tpl.description,
        definition,
      }).subscribe({
        next: (res: any) => resolve(res.id),
        error: (err: any) => reject(err),
      });
    });
  }

  addCondition(type: 'entry' | 'exit'): void {
    const cond: ConditionForm = {
      indicator: 'RSI',
      comparison: 'LessThan',
      value: 30,
      valueHigh: null,
      period: 14,
      referenceIndicator: '',
      referencePeriod: null,
      timeframe: 'Daily',
    };
    if (type === 'entry') this.entryConditions.push(cond);
    else this.exitConditions.push(cond);
  }

  removeCondition(type: 'entry' | 'exit', index: number): void {
    if (type === 'entry') this.entryConditions.splice(index, 1);
    else this.exitConditions.splice(index, 1);
  }

  addParamRange(): void {
    this.paramRanges.push({ name: '', min: 10, max: 50, step: 10 });
  }

  totalCombinations(): number {
    if (this.paramRanges.length === 0) return 0;
    return this.paramRanges.reduce((acc, p) => {
      const count = p.step > 0 ? Math.floor((p.max - p.min) / p.step) + 1 : 1;
      return acc * Math.max(count, 1);
    }, 1);
  }

  async runBacktest(): Promise<void> {
    this.running.set(true);
    this.runningLabel.set('Running backtest...');
    this.error.set('');

    try {
      const strategyId = await this.ensureStrategy();
      if (!strategyId) return;

      this.api.runBacktest({
        strategyId,
        symbol: this.symbol.toUpperCase(),
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
      this.error.set(e.message ?? 'Failed to create strategy');
    }
  }

  async runWalkForward(): Promise<void> {
    this.running.set(true);
    this.runningLabel.set('Running walk-forward analysis...');
    this.error.set('');

    try {
      const strategyId = await this.ensureStrategy();
      if (!strategyId) return;

      this.api.runBacktest({
        strategyId,
        symbol: this.symbol.toUpperCase(),
        startDate: this.startDate,
        endDate: this.endDate,
      }).subscribe({
        next: (result: any) => {
          this.running.set(false);
          this.router.navigate(['/backtests', result.id]);
        },
        error: (err: any) => {
          this.running.set(false);
          this.error.set(err.error?.detail ?? err.message ?? 'Walk-forward failed');
        },
      });
    } catch (e: any) {
      this.running.set(false);
      this.error.set(e.message ?? 'Failed');
    }
  }

  /** Checks whether the new-strategy form is valid (skipped when using an existing strategy). */
  isNewStrategyValid(): boolean {
    if (this.selectedStrategyId) return true;
    return this.entryConditions.length > 0;
  }

  /** Create strategy if new, or return selected ID */
  private ensureStrategy(): Promise<string> {
    return new Promise((resolve, reject) => {
      if (this.selectedStrategyId) {
        resolve(this.selectedStrategyId);
        return;
      }

      if (this.entryConditions.length === 0) {
        reject(new Error('Add at least one entry condition before running a backtest.'));
        return;
      }

      const definition = {
        entryConditions: this.buildConditionGroups(this.entryConditions),
        exitConditions: this.buildConditionGroups(this.exitConditions),
        stopLoss: { type: this.stopLossType, multiplier: this.stopLossMultiplier },
        takeProfit: { type: this.takeProfitType, multiplier: this.takeProfitMultiplier },
        positionSizing: {
          riskPercent: this.riskPercent,
          maxPositions: this.maxPositions,
          maxPortfolioHeat: this.maxPortfolioHeat,
          maxDrawdownPercent: this.maxDrawdown,
          drawdownRecoveryPercent: this.drawdownRecovery,
        },
        filters: {},
      };

      this.api.createStrategyV2({
        name: this.strategyName || `Strategy ${new Date().toISOString().slice(0, 10)}`,
        description: this.strategyDescription || null,
        definition,
      }).subscribe({
        next: (res: any) => {
          this.selectedStrategyId = res.id;
          resolve(res.id);
        },
        error: (err: any) => reject(err),
      });
    });
  }

  private buildConditionGroups(conditions: ConditionForm[]): any[] {
    const groups = new Map<string, any[]>();
    for (const c of conditions) {
      if (!groups.has(c.timeframe)) groups.set(c.timeframe, []);
      const cond: any = {
        indicator: c.indicator,
        comparison: c.comparison,
        value: c.value,
      };
      if (c.period) cond.period = c.period;
      if (c.valueHigh) cond.valueHigh = c.valueHigh;
      if (c.referenceIndicator) {
        cond.referenceIndicator = c.referenceIndicator;
        if (c.referencePeriod) cond.referencePeriod = c.referencePeriod;
      }
      groups.get(c.timeframe)!.push(cond);
    }

    return Array.from(groups.entries()).map(([timeframe, conds]) => ({
      timeframe,
      conditions: conds,
    }));
  }

  /** Convert template definition condition groups into flat ConditionForm arrays */
  private flattenConditions(groups: any[]): ConditionForm[] {
    const result: ConditionForm[] = [];
    for (const group of groups) {
      for (const c of group.conditions) {
        result.push({
          indicator: c.indicator,
          comparison: c.comparison,
          value: c.value ?? 0,
          valueHigh: null,
          period: c.period ?? null,
          referenceIndicator: c.referenceIndicator ?? '',
          referencePeriod: c.referencePeriod ?? null,
          timeframe: group.timeframe,
        });
      }
    }
    return result;
  }
}
