import {
  Component, inject, signal, computed, OnInit, OnDestroy,
  AfterViewInit, ElementRef, ViewChild,
} from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import {
  createChart, LineSeries, AreaSeries, HistogramSeries,
  type IChartApi, type ISeriesApi,
} from 'lightweight-charts';

// ── Interfaces ──────────────────────────────────────────────

interface EquityPoint { date: string; value: number; }
interface TradeRecord {
  symbol: string; entryDate: string; entryPrice: number;
  exitDate: string; exitPrice: number; shares: number;
  pnL: number; pnLPercent: number; commission: number;
  holdingDays: number; exitReason: string;
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
  grade: string; // Good | Warning | Overfitted
  blessedParameters: { values: Record<string, number> };
  aggregatedEquityCurve: EquityPoint[];
  elapsedTime: string;
  warnings: string[];
}
interface MonthCell { year: number; month: number; value: number; }

// ── Component ───────────────────────────────────────────────

@Component({
  selector: 'app-backtest-result',
  standalone: true,
  imports: [RouterLink, DecimalPipe, DatePipe],
  template: `
    <div class="max-w-6xl">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-4">
        <a routerLink="/backtests" class="btn btn-ghost btn-sm">&larr;</a>
        <h2 class="text-2xl font-bold">Backtest Results</h2>
      </div>

      @if (loading()) {
        <div class="flex flex-col items-center gap-3 p-12">
          <span class="loading loading-spinner loading-lg"></span>
          <span class="text-sm text-base-content/60">Loading backtest result...</span>
        </div>
      } @else if (error()) {
        <div class="alert alert-error text-sm">{{ error() }}</div>
      } @else if (result()) {

        <!-- Subheader: strategy / symbol / dates -->
        <div class="flex flex-wrap gap-3 text-sm text-base-content/60 mb-4">
          <span class="badge badge-outline">{{ result().symbol }}</span>
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

        <!-- Tabs -->
        <div role="tablist" class="tabs tabs-bordered mb-4">
          <button role="tab" class="tab" [class.tab-active]="tab() === 'equity'" (click)="setTab('equity')">Equity Curve</button>
          <button role="tab" class="tab" [class.tab-active]="tab() === 'trades'" (click)="setTab('trades')">
            Trades ({{ trades().length }})
          </button>
          <button role="tab" class="tab" [class.tab-active]="tab() === 'monthly'" (click)="setTab('monthly')">Monthly Returns</button>
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
                      </tr>
                    </thead>
                    <tbody>
                      @for (t of sortedTrades(); track $index) {
                        <tr>
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
                        </tr>
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

        <!-- Tab: Walk-Forward -->
        @if (tab() === 'wf' && walkForward()) {
          <div class="card bg-base-200">
            <div class="card-body p-4 gap-4">
              <!-- Aggregate summary -->
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

              <!-- Blessed parameters -->
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

              <!-- Per-window table -->
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
  tab = signal<'equity' | 'trades' | 'monthly' | 'wf'>('equity');

  // Parsed data
  equityCurve = signal<EquityPoint[]>([]);
  benchmarkEquity = signal<EquityPoint[]>([]);
  trades = signal<TradeRecord[]>([]);
  spyComp = signal<SpyComparison | null>(null);
  walkForward = signal<WalkForwardResult | null>(null);
  monthlyReturns = signal<Record<string, number>>({});

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

        // Render charts after view updates
        setTimeout(() => this.renderCharts(), 0);
      },
      error: (err) => {
        this.error.set(err.error?.detail ?? 'Failed to load result');
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
  }

  // ── Tab switching ─────────────────────────────────────────

  setTab(t: 'equity' | 'trades' | 'monthly' | 'wf'): void {
    this.tab.set(t);
    if (t === 'equity') {
      setTimeout(() => this.renderCharts(), 0);
    }
  }

  // ── JSON Parsing ──────────────────────────────────────────

  private parseJsonFields(res: any): void {
    this.equityCurve.set(this.safeParseJson(res.equityCurveJson, []));
    this.benchmarkEquity.set(this.safeParseJson(res.benchmarkReturnJson, []));
    this.trades.set(this.safeParseJson(res.tradeLogJson, []));
    this.spyComp.set(this.safeParseJson(res.spyComparisonJson, null));
    this.walkForward.set(this.safeParseJson(res.walkForwardJson, null));
    this.monthlyReturns.set(this.safeParseJson(res.monthlyReturnsJson, {}));
  }

  private safeParseJson<T>(json: string | null | undefined, fallback: T): T {
    if (!json) return fallback;
    try { return JSON.parse(json); } catch { return fallback; }
  }

  // ── Chart Rendering ───────────────────────────────────────

  private renderCharts(): void {
    this.renderEquityChart();
    this.renderDrawdownChart();
  }

  private renderEquityChart(): void {
    if (!this.equityChartEl?.nativeElement || this.equityCurve().length === 0) return;

    // Clean up previous chart
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

    // Strategy equity line
    const strategySeries = chart.addSeries(LineSeries, {
      color: '#570df8', lineWidth: 2, priceFormat: { type: 'custom', formatter: (v: number) => '$' + v.toLocaleString() },
    });
    strategySeries.setData(this.toLineData(this.equityCurve()));

    // SPY benchmark overlay
    if (this.benchmarkEquity().length > 0) {
      const spySeries = chart.addSeries(LineSeries, {
        color: '#fbbd23', lineWidth: 1, lineStyle: 2,
        priceFormat: { type: 'custom', formatter: (v: number) => '$' + v.toLocaleString() },
      });
      spySeries.setData(this.toLineData(this.benchmarkEquity()));
    }

    chart.timeScale().fitContent();
    this.setupResize(chart, container);
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
    const header = 'Symbol,Entry Date,Entry Price,Exit Date,Exit Price,Shares,P&L,P&L %,Commission,Holding Days,Exit Reason';
    const rows = this.trades().map(t =>
      [t.symbol, t.entryDate, t.entryPrice, t.exitDate, t.exitPrice, t.shares, t.pnL, t.pnLPercent, t.commission, t.holdingDays, `"${t.exitReason}"`].join(',')
    );
    const csv = [header, ...rows].join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `backtest-trades-${this.result()?.symbol ?? 'export'}.csv`;
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

  formatPct(value: number | null | undefined): string {
    if (value === null || value === undefined) return '—';
    return (value >= 0 ? '+' : '') + value.toFixed(2) + '%';
  }

  objectEntries(obj: Record<string, number>): [string, number][] {
    return Object.entries(obj ?? {});
  }
}
