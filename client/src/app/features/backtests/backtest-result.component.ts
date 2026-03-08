import {
  Component, inject, signal, computed, OnInit, OnDestroy,
  AfterViewInit, ElementRef, ViewChild,
} from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import {
  createChart, LineSeries, AreaSeries,
  type IChartApi,
} from 'lightweight-charts';
import { TradeChartComponent } from './trade-chart.component';
import { StrategyExplainerComponent } from './strategy-explainer.component';

// ── Interfaces ──────────────────────────────────────────────

interface EquityPoint { date: string; value: number; }
interface TradeRecord {
  symbol: string; entryDate: string; entryPrice: number;
  exitDate: string; exitPrice: number; shares: number;
  pnL: number; pnLPercent: number; commission: number;
  holdingDays: number; exitReason: string;
  reasoningJson?: string; signalScore?: number; regime?: string;
}
interface SpyComparison {
  strategyCagr: number; spyCagr: number; alpha: number; beta: number;
}
interface WfWindow {
  windowNumber: number;
  inSampleStart: string; inSampleEnd: string;
  outOfSampleStart: string; outOfSampleEnd: string;
  inSampleSharpe: number; outOfSampleSharpe: number;
  overfittingScore: number; efficiency: number;
  bestParameters: { values: Record<string, number> };
}
interface WalkForwardResult {
  windows: WfWindow[];
  averageInSampleSharpe: number; averageOutOfSampleSharpe: number;
  averageOverfittingScore: number; averageEfficiency: number;
  grade: string;
  blessedParameters: { values: Record<string, number> };
  aggregatedEquityCurve: EquityPoint[];
  elapsedTime: string;
  warnings: string[];
}
interface MonthCell { year: number; month: number; value: number; }
interface SymbolBreakdown {
  symbol: string; trades: number; wins: number;
  winRate: number; totalPnL: number; avgPnLPercent: number; avgHoldingDays: number;
}

// ── Component ───────────────────────────────────────────────

@Component({
  selector: 'app-backtest-result',
  standalone: true,
  imports: [RouterLink, DecimalPipe, DatePipe, TradeChartComponent, StrategyExplainerComponent],
  template: `
    <div class="max-w-6xl">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-4">
        <a routerLink="/backtests" class="btn btn-ghost btn-sm">&larr;</a>
        <h2 class="text-2xl font-bold">Backtest Results</h2>
        @if (isPortfolio()) {
          <span class="badge badge-outline">Portfolio</span>
        }
      </div>

      @if (loading()) {
        <div class="flex flex-col items-center gap-3 p-12">
          <span class="loading loading-spinner loading-lg"></span>
          <span class="text-sm text-base-content/60">Loading backtest result...</span>
        </div>
      } @else if (error()) {
        <div class="alert alert-error text-sm">{{ error() }}</div>
      } @else if (result()) {

        <!-- Subheader -->
        <div class="flex flex-wrap gap-3 text-sm text-base-content/60 mb-4">
          @if (isPortfolio()) {
            <span class="badge badge-outline">{{ result().universeName }}</span>
            <span class="text-xs">{{ result().symbolsWithData }}/{{ result().totalSymbols }} symbols</span>
          } @else {
            <span class="badge badge-outline">{{ result().symbol }}</span>
          }
          <span>{{ result().startDate | date:'mediumDate' }} &ndash; {{ result().endDate | date:'mediumDate' }}</span>
          <span class="badge badge-sm" [class.badge-success]="result().status === 'Completed'"
                [class.badge-warning]="result().status === 'Running'"
                [class.badge-error]="result().status === 'Failed'">{{ result().status }}</span>
        </div>

        <!-- SPY Comparison Banner -->
        @if (spyComp()) {
          <div class="alert mb-4 py-2" [class.alert-success]="spyComp()!.strategyCagr >= spyComp()!.spyCagr"
               [class.alert-error]="spyComp()!.strategyCagr < spyComp()!.spyCagr">
            <div class="flex gap-6 text-sm font-mono w-full justify-center">
              <span>Strategy: <strong>{{ formatPct(spyComp()!.strategyCagr) }}</strong></span>
              <span>vs</span>
              <span>SPY: <strong>{{ formatPct(spyComp()!.spyCagr) }}</strong></span>
              <span class="opacity-60">Alpha: {{ spyComp()!.alpha.toFixed(2) }}</span>
              <span class="opacity-60">Beta: {{ spyComp()!.beta.toFixed(2) }}</span>
            </div>
          </div>
        }

        <!-- Auto-Optimize Button + Progress -->
        <div class="mb-4">
          <div class="flex items-center gap-3">
            <button class="btn btn-primary btn-sm" (click)="runAutoOptimize()"
                    [disabled]="autoOptimizing()">
              @if (autoOptimizing()) {
                <span class="loading loading-spinner loading-xs"></span>
              }
              Auto-Optimize
            </button>
            @if (autoOptimizeError()) {
              <span class="text-error text-xs">{{ autoOptimizeError() }}</span>
            }
          </div>

          @if (autoOptimizing()) {
            <div class="mt-2 text-xs space-y-1.5">
              <div class="flex items-center gap-2">
                <span class="loading loading-spinner loading-xs"></span>
                <span class="font-semibold">{{ friendlyStep(autoOptStep()) }}</span>
                <span class="font-mono text-base-content/40">{{ autoOptElapsed() }}</span>
              </div>
              @if (autoOptProgressData(); as pd) {
                <div class="flex items-center gap-2 text-base-content/60">
                  <span>Testing period {{ pd.period }} of {{ pd.totalPeriods }}</span>
                  <span class="text-base-content/30">&middot;</span>
                  <span>{{ pd.testedLabel }} of {{ pd.totalLabel }} parameter sets tried</span>
                  <span class="text-base-content/30">&middot;</span>
                  <span class="font-semibold">{{ pd.pct }}% complete</span>
                </div>
                <progress class="progress progress-primary w-full h-1.5" [value]="pd.overallPct" max="100"></progress>
              }
            </div>
          }
        </div>

        @if (autoOptimizeResult(); as aor) {
          <div class="card bg-base-200 mb-4">
            <div class="card-body p-4 gap-3">
              <h3 class="text-sm font-semibold">Auto-Optimize Results</h3>

              <!-- Diagnosis badges -->
              <div class="flex flex-wrap gap-2">
                @for (d of aor.diagnoses; track d) {
                  <span class="badge badge-warning badge-sm">{{ d }}</span>
                }
              </div>

              <!-- Parameter ranges tested -->
              @if (aor.generatedRanges?.length) {
                <div class="text-xs text-base-content/50">
                  <span class="font-semibold">Ranges tested:</span>
                  <span class="font-mono ml-1">
                    @for (r of aor.generatedRanges; track r.name) {
                      {{ r.name }}[{{ r.min }}..{{ r.max }}]{{ $last ? '' : ', ' }}
                    }
                  </span>
                </div>
              }

              <!-- Before/After comparison table -->
              <div class="overflow-x-auto">
                <table class="table table-xs">
                  <thead>
                    <tr>
                      <th>Metric</th>
                      <th class="text-right">Before</th>
                      <th class="text-right">After (OOS)</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr>
                      <td>Win Rate</td>
                      <td class="text-right font-mono">{{ formatPct(aor.beforeWinRate) }}</td>
                      <td class="text-right font-mono">—</td>
                    </tr>
                    <tr>
                      <td>Sharpe</td>
                      <td class="text-right font-mono">{{ aor.beforeSharpe.toFixed(2) }}</td>
                      <td class="text-right font-mono"
                          [class.text-success]="aor.optimization.avgOutOfSampleSharpe > aor.beforeSharpe"
                          [class.text-error]="aor.optimization.avgOutOfSampleSharpe < aor.beforeSharpe">
                        {{ aor.optimization.avgOutOfSampleSharpe.toFixed(2) }}
                      </td>
                    </tr>
                    <tr>
                      <td>Max Drawdown</td>
                      <td class="text-right font-mono">{{ formatPct(aor.beforeMaxDrawdown) }}</td>
                      <td class="text-right font-mono">—</td>
                    </tr>
                    <tr>
                      <td>CAGR</td>
                      <td class="text-right font-mono">{{ formatPct(aor.beforeCagr) }}</td>
                      <td class="text-right font-mono">—</td>
                    </tr>
                    <tr>
                      <td>Profit Factor</td>
                      <td class="text-right font-mono">{{ aor.beforeProfitFactor.toFixed(2) }}</td>
                      <td class="text-right font-mono">—</td>
                    </tr>
                    <tr>
                      <td>Overfitting Grade</td>
                      <td class="text-right font-mono">—</td>
                      <td class="text-right font-mono">
                        <span class="badge badge-xs"
                              [class.badge-success]="aor.optimization.overfittingGrade === 'Good'"
                              [class.badge-warning]="aor.optimization.overfittingGrade === 'Warning'"
                              [class.badge-error]="aor.optimization.overfittingGrade === 'Overfitted'">
                          {{ aor.optimization.overfittingGrade }}
                        </span>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <!-- Blessed parameters -->
              @if (aor.optimization.blessedParameters) {
                <div>
                  <h4 class="text-xs font-semibold mb-1 text-base-content/60">Blessed Parameters</h4>
                  <div class="flex flex-wrap gap-2">
                    @for (p of objectEntries(aor.optimization.blessedParameters); track p[0]) {
                      <span class="badge badge-sm badge-primary badge-outline font-mono">{{ p[0] }}: {{ p[1] }}</span>
                    }
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- Optimized Parameters Badge -->
        @if (optimizedParams()) {
          <div class="collapse collapse-arrow bg-base-200 mb-4">
            <input type="checkbox" />
            <div class="collapse-title text-sm font-medium flex items-center gap-2 py-2 min-h-0">
              <span class="badge badge-sm"
                    [class.badge-success]="optimizedParams()!.overfittingGrade === 'Good'"
                    [class.badge-warning]="optimizedParams()!.overfittingGrade === 'Warning'"
                    [class.badge-error]="optimizedParams()!.overfittingGrade === 'Overfitted'">
                {{ optimizedParams()!.overfittingGrade }}
              </span>
              Optimized Parameters (v{{ optimizedParams()!.version }})
            </div>
            <div class="collapse-content">
              <div class="grid grid-cols-3 gap-2 text-xs font-mono mb-2">
                <div><span class="text-base-content/50">OOS Sharpe:</span> {{ optimizedParams()!.avgOutOfSampleSharpe.toFixed(2) }}</div>
                <div><span class="text-base-content/50">Efficiency:</span> {{ (optimizedParams()!.avgEfficiency * 100).toFixed(1) }}%</div>
                <div><span class="text-base-content/50">Windows:</span> {{ optimizedParams()!.windowCount }}</div>
              </div>
              @if (optimizedParams()!.parameters) {
                <div class="flex flex-wrap gap-1">
                  @for (p of objectEntries(optimizedParams()!.parameters); track p[0]) {
                    <span class="badge badge-xs badge-outline font-mono">{{ p[0] }}: {{ p[1] }}</span>
                  }
                </div>
              }
            </div>
          </div>
        }

        <!-- Portfolio summary cards -->
        @if (isPortfolio()) {
          <div class="grid grid-cols-3 gap-3 mb-4">
            <div class="bg-base-200 rounded-lg p-3 text-center">
              <div class="text-[10px] text-base-content/50 uppercase tracking-wider">Symbols Traded</div>
              <div class="text-lg font-bold font-mono">{{ result().uniqueSymbolsTraded ?? 0 }}</div>
            </div>
            <div class="bg-base-200 rounded-lg p-3 text-center">
              <div class="text-[10px] text-base-content/50 uppercase tracking-wider">Avg Positions</div>
              <div class="text-lg font-bold font-mono">{{ (result().averagePositionsHeld ?? 0).toFixed(1) }}</div>
            </div>
            <div class="bg-base-200 rounded-lg p-3 text-center">
              <div class="text-[10px] text-base-content/50 uppercase tracking-wider">Max Positions</div>
              <div class="text-lg font-bold font-mono">{{ result().maxPositionsHeld ?? 0 }}</div>
            </div>
          </div>
        }

        <!-- Metric Cards -->
        <div class="grid grid-cols-3 md:grid-cols-6 gap-3 mb-6">
          @for (m of metricCards(); track m.label) {
            <div class="bg-base-200 rounded-lg p-3 text-center">
              <div class="text-[10px] text-base-content/50 uppercase tracking-wider">{{ m.label }}</div>
              <div class="text-lg font-bold font-mono" [class.text-success]="m.positive === true"
                   [class.text-error]="m.positive === false">{{ m.value }}</div>
            </div>
          }
        </div>

        <!-- Strategy Explainer -->
        <app-strategy-explainer></app-strategy-explainer>

        <!-- Tabs -->
        <div role="tablist" class="tabs tabs-bordered mb-4">
          <button role="tab" class="tab" [class.tab-active]="tab() === 'equity'" (click)="setTab('equity')">Equity Curve</button>
          <button role="tab" class="tab" [class.tab-active]="tab() === 'trades'" (click)="setTab('trades')">
            Trades ({{ trades().length }})
          </button>
          <button role="tab" class="tab" [class.tab-active]="tab() === 'monthly'" (click)="setTab('monthly')">Monthly Returns</button>
          @if (isPortfolio()) {
            <button role="tab" class="tab" [class.tab-active]="tab() === 'symbols'" (click)="setTab('symbols')">Symbol Breakdown</button>
          }
          @if (executionLog().length > 0) {
            <button role="tab" class="tab" [class.tab-active]="tab() === 'log'" (click)="setTab('log')">
              Execution Log ({{ executionLog().length }})
            </button>
          }
          @if (walkForward()) {
            <button role="tab" class="tab" [class.tab-active]="tab() === 'wf'" (click)="setTab('wf')">Walk-Forward</button>
          }
        </div>

        <!-- Tab: Equity Curve + Drawdown -->
        @if (tab() === 'equity') {
          <div class="card bg-base-200">
            <div class="card-body p-4 gap-4">
              <div class="flex items-center justify-between">
                <h3 class="text-sm font-semibold">Equity Curve</h3>
                <div class="flex gap-3 text-xs text-base-content/50">
                  <span class="flex items-center gap-1"><span class="w-3 h-0.5 bg-primary inline-block"></span> Strategy</span>
                  @if (benchmarkEquity().length > 0) {
                    <span class="flex items-center gap-1"><span class="w-3 h-0.5 bg-warning inline-block"></span> SPY</span>
                  }
                </div>
              </div>
              @if (regimeTimeline().length > 0) {
                <div class="flex gap-2 text-[10px] text-base-content/50">
                  <span class="flex items-center gap-1"><span class="w-3 h-3 rounded-sm inline-block" style="background:rgba(0,200,83,0.25)"></span> Bull</span>
                  <span class="flex items-center gap-1"><span class="w-3 h-3 rounded-sm inline-block" style="background:rgba(248,114,114,0.25)"></span> Bear</span>
                  <span class="flex items-center gap-1"><span class="w-3 h-3 rounded-sm inline-block" style="background:rgba(166,173,187,0.2)"></span> Sideways</span>
                  <span class="flex items-center gap-1"><span class="w-3 h-3 rounded-sm inline-block" style="background:rgba(251,189,35,0.25)"></span> HighVol</span>
                </div>
              }
              <div #equityChart class="w-full" style="height: 320px;"></div>

              <h3 class="text-sm font-semibold mt-2">Drawdown</h3>
              <div #drawdownChart class="w-full" style="height: 160px;"></div>
            </div>
          </div>
        }

        <!-- Tab: Trades -->
        @if (tab() === 'trades') {
          <div class="card bg-base-200">
            <div class="card-body p-4 gap-3">
              <div class="flex items-center justify-between">
                <h3 class="text-sm font-semibold">Trade Log</h3>
                <button class="btn btn-xs btn-outline" (click)="exportCsv()">Export CSV</button>
              </div>

              @if (trades().length === 0) {
                <div class="text-sm text-base-content/50 p-4">No trades recorded.</div>
              } @else {
                <div class="overflow-x-auto max-h-[500px]">
                  <table class="table table-xs table-pin-rows">
                    <thead>
                      <tr>
                        <th class="cursor-pointer" (click)="sortTrades('entryDate')">Entry Date</th>
                        <th class="cursor-pointer" (click)="sortTrades('exitDate')">Exit Date</th>
                        <th>Symbol</th>
                        <th class="text-right">Entry</th>
                        <th class="text-right">Exit</th>
                        <th class="text-right">Shares</th>
                        <th class="text-right cursor-pointer" (click)="sortTrades('pnL')">P&amp;L</th>
                        <th class="text-right cursor-pointer" (click)="sortTrades('pnLPercent')">P&amp;L %</th>
                        <th class="text-right cursor-pointer" (click)="sortTrades('holdingDays')">Days</th>
                        <th class="cursor-pointer" (click)="sortTrades('exitReason')">Exit Reason</th>
                        @if (hasTradeReasoning()) {
                          <th class="text-right">Score</th>
                        }
                        <th></th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (t of sortedTrades(); track $index) {
                        <tr class="hover cursor-pointer" (click)="toggleTradeDetail($index)">
                          <td class="font-mono text-xs">{{ t.entryDate | date:'shortDate' }}</td>
                          <td class="font-mono text-xs">{{ t.exitDate | date:'shortDate' }}</td>
                          <td class="font-mono">{{ t.symbol }}</td>
                          <td class="text-right font-mono">{{ t.entryPrice | number:'1.2-2' }}</td>
                          <td class="text-right font-mono">{{ t.exitPrice | number:'1.2-2' }}</td>
                          <td class="text-right font-mono">{{ t.shares }}</td>
                          <td class="text-right font-mono" [class.text-success]="t.pnL > 0" [class.text-error]="t.pnL < 0">
                            {{ t.pnL | number:'1.2-2' }}
                          </td>
                          <td class="text-right font-mono" [class.text-success]="t.pnLPercent > 0" [class.text-error]="t.pnLPercent < 0">
                            {{ t.pnLPercent | number:'1.2-2' }}%
                          </td>
                          <td class="text-right font-mono">{{ t.holdingDays }}</td>
                          <td class="text-xs">{{ t.exitReason }}</td>
                          @if (hasTradeReasoning()) {
                            <td class="text-right font-mono text-xs">{{ (t.signalScore ?? 0) | number:'1.0-0' }}</td>
                          }
                          <td>
                            <button class="btn btn-ghost btn-xs" (click)="openTradeChart($index, $event)" title="View chart">
                              chart
                            </button>
                          </td>
                        </tr>
                        @if (expandedTradeIdx() === $index && t.reasoningJson) {
                          <tr>
                            <td [attr.colspan]="hasTradeReasoning() ? 11 : 10" class="bg-base-300 p-3">
                              <div class="text-xs space-y-1">
                                @if (t.regime) {
                                  <div><span class="text-base-content/50">Regime:</span> {{ t.regime }}</div>
                                }
                                @if (parseReasoning(t.reasoningJson); as r) {
                                  <div><span class="text-base-content/50">Score:</span> {{ r.compositeScore | number:'1.1-1' }}</div>
                                  @if (r.conditionsFired?.length) {
                                    <div><span class="text-base-content/50">Conditions:</span> {{ r.conditionsFired.join(', ') }}</div>
                                  }
                                  @if (r.factorContributions) {
                                    <div class="flex gap-3 flex-wrap">
                                      @for (f of objectEntries(r.factorContributions); track f[0]) {
                                        <span class="badge badge-xs badge-outline font-mono">{{ f[0] }}: {{ f[1] | number:'1.1-1' }}</span>
                                      }
                                    </div>
                                  }
                                }
                              </div>
                            </td>
                          </tr>
                        }
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        }

        <!-- Tab: Monthly Returns Heatmap -->
        @if (tab() === 'monthly') {
          <div class="card bg-base-200">
            <div class="card-body p-4 gap-3">
              <h3 class="text-sm font-semibold">Monthly Returns Heatmap</h3>

              @if (heatmapYears().length === 0) {
                <div class="text-sm text-base-content/50 p-4">No monthly return data available.</div>
              } @else {
                <div class="overflow-x-auto">
                  <table class="table table-xs">
                    <thead>
                      <tr>
                        <th>Year</th>
                        @for (m of monthLabels; track m) {
                          <th class="text-center w-16">{{ m }}</th>
                        }
                        <th class="text-center w-20 font-bold">Annual</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (yr of heatmapYears(); track yr) {
                        <tr>
                          <td class="font-bold font-mono">{{ yr }}</td>
                          @for (m of monthIndices; track m) {
                            <td class="text-center font-mono text-xs"
                                [style.background-color]="cellColor(getMonthReturn(yr, m))"
                                [style.color]="cellTextColor(getMonthReturn(yr, m))">
                              {{ getMonthReturn(yr, m) !== null ? formatPct(getMonthReturn(yr, m)!) : '' }}
                            </td>
                          }
                          <td class="text-center font-mono text-xs font-bold"
                              [style.background-color]="cellColor(getAnnualReturn(yr))"
                              [style.color]="cellTextColor(getAnnualReturn(yr))">
                            {{ getAnnualReturn(yr) !== null ? formatPct(getAnnualReturn(yr)!) : '' }}
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        }

        <!-- Tab: Symbol Breakdown (portfolio only) -->
        @if (tab() === 'symbols' && isPortfolio()) {
          <div class="card bg-base-200">
            <div class="card-body p-4 gap-3">
              <h3 class="text-sm font-semibold">Per-Symbol Breakdown</h3>
              @if (symbolBreakdowns().length === 0) {
                <div class="text-sm text-base-content/50 p-4">No symbol breakdown data.</div>
              } @else {
                <div class="overflow-x-auto">
                  <table class="table table-xs">
                    <thead>
                      <tr>
                        <th>Symbol</th>
                        <th class="text-right">Trades</th>
                        <th class="text-right">Wins</th>
                        <th class="text-right">Win Rate</th>
                        <th class="text-right">Total P&amp;L</th>
                        <th class="text-right">Avg P&amp;L %</th>
                        <th class="text-right">Avg Days</th>
                      </tr>
                    </thead>
                    <tbody>
                      @for (s of symbolBreakdowns(); track s.symbol) {
                        <tr>
                          <td class="font-mono font-medium">{{ s.symbol }}</td>
                          <td class="text-right font-mono">{{ s.trades }}</td>
                          <td class="text-right font-mono">{{ s.wins }}</td>
                          <td class="text-right font-mono" [class.text-success]="s.winRate > 50" [class.text-error]="s.winRate < 30">
                            {{ s.winRate | number:'1.1-1' }}%
                          </td>
                          <td class="text-right font-mono" [class.text-success]="s.totalPnL > 0" [class.text-error]="s.totalPnL < 0">
                            {{ s.totalPnL | number:'1.2-2' }}
                          </td>
                          <td class="text-right font-mono">{{ s.avgPnLPercent | number:'1.2-2' }}%</td>
                          <td class="text-right font-mono">{{ s.avgHoldingDays | number:'1.1-1' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        }

        <!-- Tab: Execution Log -->
        @if (tab() === 'log') {
          <div class="card bg-base-200">
            <div class="card-body p-4 gap-3">
              <h3 class="text-sm font-semibold">Execution Log</h3>
              <div class="max-h-[500px] overflow-y-auto">
                <pre class="text-xs font-mono whitespace-pre-wrap text-base-content/70">{{ executionLog().join('\\n') }}</pre>
              </div>
            </div>
          </div>
        }

        <!-- Tab: Walk-Forward -->
        @if (tab() === 'wf' && walkForward()) {
          <div class="card bg-base-200">
            <div class="card-body p-4 gap-4">
              <div class="flex flex-wrap gap-4 items-center">
                <h3 class="text-sm font-semibold">Walk-Forward Analysis</h3>
                <span class="badge text-xs"
                      [class.badge-success]="walkForward()!.grade === 'Good'"
                      [class.badge-warning]="walkForward()!.grade === 'Warning'"
                      [class.badge-error]="walkForward()!.grade === 'Overfitted'">
                  {{ walkForward()!.grade }}
                </span>
              </div>

              <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
                <div class="bg-base-300 rounded p-2 text-center">
                  <div class="text-[10px] text-base-content/50 uppercase">Avg IS Sharpe</div>
                  <div class="font-mono font-bold">{{ walkForward()!.averageInSampleSharpe.toFixed(2) }}</div>
                </div>
                <div class="bg-base-300 rounded p-2 text-center">
                  <div class="text-[10px] text-base-content/50 uppercase">Avg OOS Sharpe</div>
                  <div class="font-mono font-bold">{{ walkForward()!.averageOutOfSampleSharpe.toFixed(2) }}</div>
                </div>
                <div class="bg-base-300 rounded p-2 text-center">
                  <div class="text-[10px] text-base-content/50 uppercase">Avg Efficiency</div>
                  <div class="font-mono font-bold">{{ formatPct(walkForward()!.averageEfficiency * 100) }}</div>
                </div>
                <div class="bg-base-300 rounded p-2 text-center">
                  <div class="text-[10px] text-base-content/50 uppercase">Overfitting Score</div>
                  <div class="font-mono font-bold">{{ formatPct(walkForward()!.averageOverfittingScore * 100) }}</div>
                </div>
              </div>

              @if (walkForward()!.blessedParameters && walkForward()!.blessedParameters.values) {
                <div>
                  <h4 class="text-xs font-semibold mb-1 text-base-content/60">Blessed Parameters</h4>
                  <div class="flex flex-wrap gap-2">
                    @for (p of objectEntries(walkForward()!.blessedParameters.values); track p[0]) {
                      <span class="badge badge-sm badge-outline font-mono">{{ p[0] }}: {{ p[1] }}</span>
                    }
                  </div>
                </div>
              }

              <div class="overflow-x-auto">
                <table class="table table-xs">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>IS Period</th>
                      <th>OOS Period</th>
                      <th class="text-right">IS Sharpe</th>
                      <th class="text-right">OOS Sharpe</th>
                      <th class="text-right">Efficiency</th>
                      <th class="text-right">Overfit</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (w of walkForward()!.windows; track w.windowNumber) {
                      <tr>
                        <td class="font-mono">{{ w.windowNumber }}</td>
                        <td class="text-xs font-mono">{{ w.inSampleStart | date:'shortDate' }} – {{ w.inSampleEnd | date:'shortDate' }}</td>
                        <td class="text-xs font-mono">{{ w.outOfSampleStart | date:'shortDate' }} – {{ w.outOfSampleEnd | date:'shortDate' }}</td>
                        <td class="text-right font-mono">{{ w.inSampleSharpe.toFixed(2) }}</td>
                        <td class="text-right font-mono">{{ w.outOfSampleSharpe.toFixed(2) }}</td>
                        <td class="text-right font-mono" [class.text-success]="w.efficiency >= 0.5" [class.text-error]="w.efficiency < 0.5">
                          {{ formatPct(w.efficiency * 100) }}
                        </td>
                        <td class="text-right font-mono" [class.text-success]="w.overfittingScore < 0.3"
                            [class.text-warning]="w.overfittingScore >= 0.3 && w.overfittingScore < 0.5"
                            [class.text-error]="w.overfittingScore >= 0.5">
                          {{ formatPct(w.overfittingScore * 100) }}
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>

              @if (walkForward()!.warnings && walkForward()!.warnings.length) {
                <div class="text-xs text-warning">
                  @for (w of walkForward()!.warnings; track $index) {
                    <div>{{ w }}</div>
                  }
                </div>
              }
            </div>
          </div>
        }
      }

      <!-- Trade Chart Modal -->
      @if (tradeChartRunId() && tradeChartIdx() !== null) {
        <app-trade-chart
          [backtestRunId]="tradeChartRunId()!"
          [tradeIndex]="tradeChartIdx()!"
          (close)="closeTradeChart()">
        </app-trade-chart>
      }
    </div>
  `,
})
export class BacktestResultComponent implements OnInit, AfterViewInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private api = inject(ApiService);

  @ViewChild('equityChart') equityChartEl!: ElementRef<HTMLDivElement>;
  @ViewChild('drawdownChart') drawdownChartEl!: ElementRef<HTMLDivElement>;

  // State
  loading = signal(true);
  error = signal('');
  result = signal<any>(null);
  tab = signal<'equity' | 'trades' | 'monthly' | 'symbols' | 'log' | 'wf'>('equity');
  optimizedParams = signal<any>(null);

  // Parsed data
  equityCurve = signal<EquityPoint[]>([]);
  benchmarkEquity = signal<EquityPoint[]>([]);
  trades = signal<TradeRecord[]>([]);
  spyComp = signal<SpyComparison | null>(null);
  walkForward = signal<WalkForwardResult | null>(null);
  monthlyReturns = signal<Record<string, number>>({});
  symbolBreakdowns = signal<SymbolBreakdown[]>([]);
  executionLog = signal<string[]>([]);

  // Auto-optimize
  autoOptimizing = signal(false);
  autoOptimizeResult = signal<any>(null);
  autoOptimizeError = signal('');
  autoOptStep = signal('');
  autoOptProgressRaw = signal<any>(null);
  autoOptElapsed = signal('');

  autoOptProgressData = computed(() => {
    const p = this.autoOptProgressRaw();
    if (!p || !p.totalWindows || p.totalWindows === 0) return null;
    const pct = p.totalCombos > 0
      ? Math.round((p.completedCombos / p.totalCombos) * 100) : 0;
    // Overall progress across all windows
    const overallPct = Math.round(
      ((p.windowIndex * p.totalCombos + p.completedCombos) / (p.totalWindows * p.totalCombos)) * 100
    );
    return {
      period: p.windowIndex + 1,
      totalPeriods: p.totalWindows,
      testedLabel: this.formatNumber(p.completedCombos),
      totalLabel: this.formatNumber(p.totalCombos),
      pct,
      overallPct,
    };
  });
  private autoOptTimer: any = null;
  private autoOptPollTimer: any = null;
  private autoOptElapsedTimer: any = null;
  private autoOptStartTime = 0;
  private wakeLock: any = null;

  // Trade detail expansion
  expandedTradeIdx = signal<number | null>(null);

  // Trade chart modal
  tradeChartRunId = signal<string | null>(null);
  tradeChartIdx = signal<number | null>(null);

  // Regime timeline
  regimeTimeline = signal<{ date: string; regime: string }[]>([]);

  // Trade sorting
  tradeSortKey = signal<keyof TradeRecord>('entryDate');
  tradeSortAsc = signal(true);

  // Charts
  private equityChartApi: IChartApi | null = null;
  private drawdownChartApi: IChartApi | null = null;
  private resizeObs: ResizeObserver | null = null;

  // Heatmap helpers
  monthLabels = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  monthIndices = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

  // ── Computed ──────────────────────────────────────────────

  isPortfolio = computed(() => {
    const r = this.result();
    return r && (r.uniqueSymbolsTraded > 1 || r.universeName);
  });

  hasTradeReasoning = computed(() => {
    return this.trades().some(t => t.reasoningJson || t.signalScore);
  });

  metricCards = computed(() => {
    const r = this.result();
    if (!r) return [];
    return [
      { label: 'CAGR', value: this.formatPct(r.cagr), positive: r.cagr > 0 ? true : r.cagr < 0 ? false : null },
      { label: 'Sharpe', value: r.sharpeRatio?.toFixed(2) ?? '—', positive: r.sharpeRatio >= 1 ? true : r.sharpeRatio < 0.5 ? false : null },
      { label: 'Sortino', value: r.sortinoRatio?.toFixed(2) ?? '—', positive: r.sortinoRatio >= 1.5 ? true : null },
      { label: 'Max DD', value: this.formatPct(r.maxDrawdown), positive: r.maxDrawdown > -15 ? true : r.maxDrawdown < -30 ? false : null },
      { label: 'Win Rate', value: this.formatPct(r.winRate), positive: r.winRate > 50 ? true : r.winRate < 30 ? false : null },
      { label: 'Profit Factor', value: r.profitFactor?.toFixed(2) ?? '—', positive: r.profitFactor > 1.5 ? true : r.profitFactor < 1 ? false : null },
    ];
  });

  sortedTrades = computed(() => {
    const list = [...this.trades()];
    const key = this.tradeSortKey();
    const asc = this.tradeSortAsc();
    list.sort((a, b) => {
      const va = a[key]; const vb = b[key];
      if (typeof va === 'number' && typeof vb === 'number') return asc ? va - vb : vb - va;
      return asc ? String(va).localeCompare(String(vb)) : String(vb).localeCompare(String(va));
    });
    return list;
  });

  heatmapYears = computed(() => {
    const m = this.monthlyReturns();
    const years = new Set<number>();
    for (const key of Object.keys(m)) {
      const y = parseInt(key.split('-')[0], 10);
      if (!isNaN(y)) years.add(y);
    }
    return [...years].sort();
  });

  // ── Lifecycle ─────────────────────────────────────────────

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) { this.loading.set(false); this.error.set('No backtest ID'); return; }

    this.api.getBacktestResult(id).subscribe({
      next: (res) => {
        this.result.set(res);
        this.parseJsonFields(res);
        this.loading.set(false);
        setTimeout(() => this.renderCharts(), 0);
        // Fetch optimized params for this strategy
        if (res.strategyId) {
          this.api.getOptimizedParams(res.strategyId).subscribe({
            next: (params: any) => this.optimizedParams.set(params?.current ?? null),
            error: () => {},
          });
        }
      },
      error: (err) => {
        this.error.set(err.error?.detail ?? 'Failed to load backtest result');
        this.loading.set(false);
      },
    });
  }

  ngAfterViewInit(): void {
    // Charts rendered in ngOnInit callback after data loads
  }

  ngOnDestroy(): void {
    this.resizeObs?.disconnect();
    this.equityChartApi?.remove();
    this.drawdownChartApi?.remove();
    this.clearAutoOptTimer();
  }

  // ── Tab switching ─────────────────────────────────────────

  setTab(t: 'equity' | 'trades' | 'monthly' | 'symbols' | 'log' | 'wf'): void {
    this.tab.set(t);
    if (t === 'equity') {
      setTimeout(() => this.renderCharts(), 0);
    }
  }

  // ── Trade detail expansion ────────────────────────────────

  toggleTradeDetail(idx: number): void {
    this.expandedTradeIdx.set(this.expandedTradeIdx() === idx ? null : idx);
  }

  parseReasoning(json: string | undefined): any {
    if (!json) return null;
    try { return JSON.parse(json); } catch { return null; }
  }

  openTradeChart(tradeIdx: number, event: Event): void {
    event.stopPropagation();
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.tradeChartRunId.set(id);
    this.tradeChartIdx.set(tradeIdx);
  }

  closeTradeChart(): void {
    this.tradeChartRunId.set(null);
    this.tradeChartIdx.set(null);
  }

  // ── Auto-Optimize ────────────────────────────────────────

  runAutoOptimize(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.autoOptimizing.set(true);
    this.autoOptimizeError.set('');
    this.autoOptimizeResult.set(null);
    this.autoOptStep.set('Starting...');
    this.autoOptProgressRaw.set(null);

    // Request wake lock to prevent sleep
    this.requestWakeLock();

    // Elapsed timer
    this.autoOptStartTime = Date.now();
    this.autoOptElapsed.set('');
    this.autoOptElapsedTimer = setInterval(() => {
      const secs = Math.floor((Date.now() - this.autoOptStartTime) / 1000);
      const m = Math.floor(secs / 60);
      const s = secs % 60;
      this.autoOptElapsed.set(m > 0 ? `${m}m ${s}s` : `${s}s`);
    }, 1000);

    // Poll progress every 1.5s
    this.autoOptPollTimer = setInterval(() => {
      this.api.getAutoOptimizeProgress(id).subscribe({
        next: (p: any) => {
          if (p.step && p.step !== 'idle') {
            this.autoOptStep.set(p.step);
            if (p.step === 'Running walk-forward optimization' && p.totalWindows > 0) {
              this.autoOptProgressRaw.set(p);
            } else {
              this.autoOptProgressRaw.set(null);
            }
          }
        },
        error: () => {},
      });
    }, 1500);

    this.api.autoOptimize(id).subscribe({
      next: (res) => {
        this.clearAutoOptTimer();
        this.autoOptimizeResult.set(res);
        this.autoOptimizing.set(false);
        const r = this.result();
        if (r?.strategyId) {
          this.api.getOptimizedParams(r.strategyId).subscribe({
            next: (params: any) => this.optimizedParams.set(params?.current ?? null),
            error: () => {},
          });
        }
      },
      error: (err) => {
        this.clearAutoOptTimer();
        this.autoOptimizeError.set(err.error?.detail ?? err.error?.message ?? 'Auto-optimize failed');
        this.autoOptimizing.set(false);
      },
    });
  }

  private clearAutoOptTimer(): void {
    if (this.autoOptTimer) {
      clearTimeout(this.autoOptTimer);
      this.autoOptTimer = null;
    }
    if (this.autoOptPollTimer) {
      clearInterval(this.autoOptPollTimer);
      this.autoOptPollTimer = null;
    }
    if (this.autoOptElapsedTimer) {
      clearInterval(this.autoOptElapsedTimer);
      this.autoOptElapsedTimer = null;
    }
    this.releaseWakeLock();
  }

  private async requestWakeLock(): Promise<void> {
    try {
      if ('wakeLock' in navigator) {
        this.wakeLock = await (navigator as any).wakeLock.request('screen');
      }
    } catch { /* wake lock not available or denied */ }
  }

  private releaseWakeLock(): void {
    try {
      this.wakeLock?.release();
      this.wakeLock = null;
    } catch { /* ignore */ }
  }

  // ── JSON Parsing ──────────────────────────────────────────

  private parseJsonFields(res: any): void {
    this.equityCurve.set(this.safeParseArray(res.equityCurveJson));
    this.benchmarkEquity.set(this.safeParseArray(res.benchmarkReturnJson));
    this.trades.set(this.safeParseArray(res.tradeLogJson));
    this.spyComp.set(this.safeParseObject<SpyComparison>(res.spyComparisonJson));
    this.walkForward.set(this.safeParseObject<WalkForwardResult>(res.walkForwardJson));
    this.monthlyReturns.set(this.safeParseJson(res.monthlyReturnsJson, {}));
    this.executionLog.set(this.safeParseArray(res.executionLogJson));

    // Symbol breakdowns
    const breakdownData = this.safeParseJson<Record<string, SymbolBreakdown>>(res.symbolBreakdownJson, {});
    this.symbolBreakdowns.set(Object.values(breakdownData));

    // Regime timeline
    this.regimeTimeline.set(this.safeParseArray(res.regimeTimelineJson));
  }

  private safeParseJson<T>(json: string | null | undefined, fallback: T): T {
    if (!json) return fallback;
    try { return JSON.parse(json); } catch { return fallback; }
  }

  private safeParseArray<T>(json: string | null | undefined): T[] {
    if (!json) return [];
    try {
      const parsed = JSON.parse(json);
      return Array.isArray(parsed) ? parsed : [];
    } catch { return []; }
  }

  private safeParseObject<T>(json: string | null | undefined): T | null {
    if (!json) return null;
    try {
      const parsed = JSON.parse(json);
      if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) return null;
      return Object.keys(parsed).length > 0 ? parsed : null;
    } catch { return null; }
  }

  // ── Chart Rendering ───────────────────────────────────────

  private renderCharts(): void {
    this.renderEquityChart();
    this.renderDrawdownChart();
  }

  private renderEquityChart(): void {
    if (!this.equityChartEl?.nativeElement || this.equityCurve().length === 0) return;

    this.equityChartApi?.remove();

    const container = this.equityChartEl.nativeElement;
    const chart = createChart(container, {
      layout: { background: { color: 'transparent' }, textColor: '#a6adbb' },
      grid: { vertLines: { color: '#2a2e37' }, horzLines: { color: '#2a2e37' } },
      crosshair: { mode: 0 },
      rightPriceScale: { borderColor: '#2a2e37' },
      timeScale: { borderColor: '#2a2e37' },
      width: container.clientWidth,
      height: 320,
    });
    this.equityChartApi = chart;

    const strategySeries = chart.addSeries(LineSeries, {
      color: '#570df8', lineWidth: 2, priceFormat: { type: 'custom', formatter: (v: number) => '$' + v.toLocaleString() },
    });
    strategySeries.setData(this.toLineData(this.equityCurve()));

    if (this.benchmarkEquity().length > 0) {
      const spySeries = chart.addSeries(LineSeries, {
        color: '#fbbd23', lineWidth: 1, lineStyle: 2,
        priceFormat: { type: 'custom', formatter: (v: number) => '$' + v.toLocaleString() },
      });
      spySeries.setData(this.toLineData(this.benchmarkEquity()));
    }

    // Regime timeline bands (rendered as separate colored area series)
    this.renderRegimeBands(chart, strategySeries);

    chart.timeScale().fitContent();
    this.setupResize(chart, container);
  }

  private renderRegimeBands(chart: IChartApi, mainSeries: any): void {
    const timeline = this.regimeTimeline();
    if (timeline.length === 0) return;

    const equityData = this.toLineData(this.equityCurve());
    if (equityData.length === 0) return;

    // Find min/max equity for band positioning
    const minVal = Math.min(...equityData.map(p => p.value));
    const maxVal = Math.max(...equityData.map(p => p.value));

    const regimeColors: Record<string, { top: string; bottom: string; line: string }> = {
      'Bull': { top: 'rgba(0,200,83,0.12)', bottom: 'rgba(0,200,83,0.02)', line: 'rgba(0,200,83,0.0)' },
      'Bear': { top: 'rgba(248,114,114,0.12)', bottom: 'rgba(248,114,114,0.02)', line: 'rgba(248,114,114,0.0)' },
      'Sideways': { top: 'rgba(166,173,187,0.08)', bottom: 'rgba(166,173,187,0.01)', line: 'rgba(166,173,187,0.0)' },
      'HighVol': { top: 'rgba(251,189,35,0.12)', bottom: 'rgba(251,189,35,0.02)', line: 'rgba(251,189,35,0.0)' },
    };

    // Group contiguous regime spans
    for (let i = 0; i < timeline.length; i++) {
      const entry = timeline[i];
      const startDate = (entry.date || (entry as any).Date || (entry as any).item1)?.substring(0, 10);
      const regime = entry.regime || (entry as any).Regime || (entry as any).item2 || 'Sideways';
      const nextDate = i + 1 < timeline.length
        ? (timeline[i + 1].date || (timeline[i + 1] as any).Date || (timeline[i + 1] as any).item1)?.substring(0, 10)
        : equityData[equityData.length - 1].time;

      if (!startDate || !nextDate) continue;

      const colors = regimeColors[regime] ?? regimeColors['Sideways'];

      // Create a band using area series spanning this regime period
      const bandSeries = chart.addSeries(AreaSeries, {
        topColor: colors.top,
        bottomColor: colors.bottom,
        lineColor: colors.line,
        lineWidth: 1,
        priceScaleId: 'regime',
        lastValueVisible: false,
        priceLineVisible: false,
      });

      // Build data points for this regime span
      const bandData = equityData
        .filter(p => p.time >= startDate && p.time <= nextDate)
        .map(p => ({ time: p.time, value: maxVal * 1.1 }));

      if (bandData.length > 0) {
        bandSeries.setData(bandData);
      }
    }

    // Hide the regime price scale
    chart.priceScale('regime').applyOptions({ visible: false });
  }

  private renderDrawdownChart(): void {
    if (!this.drawdownChartEl?.nativeElement || this.equityCurve().length === 0) return;

    this.drawdownChartApi?.remove();

    const container = this.drawdownChartEl.nativeElement;
    const chart = createChart(container, {
      layout: { background: { color: 'transparent' }, textColor: '#a6adbb' },
      grid: { vertLines: { color: '#2a2e37' }, horzLines: { color: '#2a2e37' } },
      crosshair: { mode: 0 },
      rightPriceScale: { borderColor: '#2a2e37' },
      timeScale: { borderColor: '#2a2e37' },
      width: container.clientWidth,
      height: 160,
    });
    this.drawdownChartApi = chart;

    const ddSeries = chart.addSeries(AreaSeries, {
      lineColor: '#f87272', topColor: 'rgba(248,114,114,0.3)', bottomColor: 'rgba(248,114,114,0.02)',
      lineWidth: 1,
      priceFormat: { type: 'custom', formatter: (v: number) => v.toFixed(1) + '%' },
    });

    ddSeries.setData(this.computeDrawdown(this.equityCurve()));
    chart.timeScale().fitContent();
  }

  private toLineData(points: EquityPoint[]): { time: string; value: number }[] {
    return points
      .filter(p => p.date || (p as any).Date)
      .map(p => ({
        time: (p.date || (p as any).Date).substring(0, 10),
        value: p.value ?? (p as any).Value ?? 0,
      }))
      .sort((a, b) => a.time.localeCompare(b.time));
  }

  private computeDrawdown(points: EquityPoint[]): { time: string; value: number }[] {
    const line = this.toLineData(points);
    let peak = -Infinity;
    return line.map(p => {
      if (p.value > peak) peak = p.value;
      const dd = peak > 0 ? ((p.value - peak) / peak) * 100 : 0;
      return { time: p.time, value: dd };
    });
  }

  private setupResize(chart: IChartApi, container: HTMLDivElement): void {
    if (!this.resizeObs) {
      this.resizeObs = new ResizeObserver(() => {
        this.equityChartApi?.applyOptions({ width: this.equityChartEl?.nativeElement?.clientWidth ?? 0 });
        this.drawdownChartApi?.applyOptions({ width: this.drawdownChartEl?.nativeElement?.clientWidth ?? 0 });
      });
    }
    this.resizeObs.observe(container);
  }

  // ── Trade Sorting ─────────────────────────────────────────

  sortTrades(key: keyof TradeRecord): void {
    if (this.tradeSortKey() === key) {
      this.tradeSortAsc.set(!this.tradeSortAsc());
    } else {
      this.tradeSortKey.set(key);
      this.tradeSortAsc.set(key === 'entryDate');
    }
  }

  // ── CSV Export ─────────────────────────────────────────────

  exportCsv(): void {
    const header = 'Symbol,Entry Date,Entry Price,Exit Date,Exit Price,Shares,P&L,P&L %,Commission,Holding Days,Exit Reason,Signal Score';
    const rows = this.trades().map(t =>
      [t.symbol, t.entryDate, t.entryPrice, t.exitDate, t.exitPrice, t.shares, t.pnL, t.pnLPercent, t.commission, t.holdingDays, `"${t.exitReason}"`, t.signalScore ?? ''].join(',')
    );
    const csv = [header, ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `backtest-trades-${this.result()?.symbol ?? 'portfolio'}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }

  // ── Monthly Heatmap ───────────────────────────────────────

  getMonthReturn(year: number, month: number): number | null {
    const key = `${year}-${String(month).padStart(2, '0')}`;
    const val = this.monthlyReturns()[key];
    return val !== undefined ? val : null;
  }

  getAnnualReturn(year: number): number | null {
    let sum = 0; let count = 0;
    for (let m = 1; m <= 12; m++) {
      const v = this.getMonthReturn(year, m);
      if (v !== null) { sum += v; count++; }
    }
    return count > 0 ? sum : null;
  }

  cellColor(value: number | null): string {
    if (value === null) return 'transparent';
    const intensity = Math.min(Math.abs(value) / 10, 1);
    if (value >= 0) return `rgba(0, 200, 83, ${0.15 + intensity * 0.55})`;
    return `rgba(248, 114, 114, ${0.15 + intensity * 0.55})`;
  }

  cellTextColor(value: number | null): string {
    if (value === null) return 'inherit';
    const intensity = Math.min(Math.abs(value) / 10, 1);
    return intensity > 0.5 ? '#fff' : 'inherit';
  }

  // ── Helpers ───────────────────────────────────────────────

  friendlyStep(step: string): string {
    const map: Record<string, string> = {
      '': 'Starting...',
      'Loading backtest data': 'Loading your backtest results...',
      'Analyzing trade log': 'Analyzing your trades for patterns...',
      'Diagnosing weaknesses': 'Identifying what can be improved...',
      'Fetching market data': 'Downloading price data from Yahoo Finance...',
      'Running walk-forward optimization': 'Testing parameter combinations...',
    };
    return map[step] ?? step;
  }

  formatNumber(n: number): string {
    if (n >= 1_000_000) return (n / 1_000_000).toFixed(1) + 'M';
    if (n >= 1_000) return (n / 1_000).toFixed(1) + 'K';
    return n.toLocaleString();
  }

  formatPct(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return (value >= 0 ? '+' : '') + value.toFixed(2) + '%';
  }

  objectEntries(obj: Record<string, number>): [string, number][] {
    return Object.entries(obj ?? {});
  }
}
