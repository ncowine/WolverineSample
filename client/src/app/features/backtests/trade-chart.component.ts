import {
  Component, inject, signal, Input, Output, EventEmitter,
  AfterViewInit, OnDestroy, ElementRef, ViewChild, OnChanges,
} from '@angular/core';
import { DecimalPipe, DatePipe, PercentPipe } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import {
  createChart, CandlestickSeries, LineSeries, HistogramSeries, createSeriesMarkers,
  type IChartApi, type ISeriesApi, type ISeriesMarkersPluginApi, type Time,
} from 'lightweight-charts';

interface TradeReasoning {
  conditionsFired: string[];
  compositeScore: number;
  factorContributions: Record<string, number>;
  regime: string;
  regimeConfidence: number;
}

@Component({
  selector: 'app-trade-chart',
  standalone: true,
  imports: [DecimalPipe, DatePipe],
  template: `
    <div class="modal modal-open" (click)="close.emit()">
      <div class="modal-box max-w-6xl w-full" (click)="$event.stopPropagation()">

        @if (loading()) {
          <div class="flex items-center justify-center p-12">
            <span class="loading loading-spinner loading-md"></span>
          </div>
        } @else if (error()) {
          <div class="alert alert-error text-sm">{{ error() }}</div>
        } @else if (data()) {
          <!-- Header row -->
          <div class="flex items-center justify-between mb-2">
            <div class="flex items-center gap-3">
              <h3 class="font-bold text-lg font-mono">{{ data()!.symbol }}</h3>
              <span class="badge" [class.badge-success]="data()!.pnL > 0" [class.badge-error]="data()!.pnL <= 0">
                {{ data()!.pnL > 0 ? '+' : '' }}{{ data()!.pnL | number:'1.2-2' }}
                ({{ data()!.pnLPercent > 0 ? '+' : '' }}{{ data()!.pnLPercent | number:'1.1-1' }}%)
              </span>
            </div>
            <button class="btn btn-sm btn-ghost" (click)="close.emit()">X</button>
          </div>

          <!-- Summary strip -->
          <div class="grid grid-cols-3 md:grid-cols-6 gap-2 mb-3 text-xs">
            <div class="bg-base-200 rounded px-2 py-1.5">
              <span class="text-base-content/50">Entry</span>
              <div class="font-mono">{{ data()!.entryPrice | number:'1.2-2' }}</div>
              <div class="text-base-content/40">{{ data()!.entryDate | date:'M/d' }}</div>
            </div>
            <div class="bg-base-200 rounded px-2 py-1.5">
              <span class="text-base-content/50">Exit</span>
              <div class="font-mono">{{ data()!.exitPrice | number:'1.2-2' }}</div>
              <div class="text-base-content/40">{{ data()!.exitDate | date:'M/d' }}</div>
            </div>
            <div class="bg-base-200 rounded px-2 py-1.5">
              <span class="text-base-content/50">Shares</span>
              <div class="font-mono">{{ data()!.shares }}</div>
            </div>
            <div class="bg-base-200 rounded px-2 py-1.5">
              <span class="text-base-content/50">Regime</span>
              <div>
                @if (reasoning()) {
                  <span class="badge badge-xs" [class.badge-success]="reasoning()!.regime === 'Bull'"
                    [class.badge-warning]="reasoning()!.regime === 'Sideways'"
                    [class.badge-error]="reasoning()!.regime === 'Bear'">{{ reasoning()!.regime }}</span>
                  <span class="text-base-content/40 ml-1">({{ reasoning()!.regimeConfidence * 100 | number:'1.0-0' }}%)</span>
                } @else if (data()!.regime) {
                  <span class="badge badge-xs badge-outline">{{ data()!.regime }}</span>
                }
              </div>
            </div>
            <div class="bg-base-200 rounded px-2 py-1.5">
              <span class="text-base-content/50">Score</span>
              <div class="font-mono">{{ data()!.signalScore | number:'1.0-0' }}</div>
            </div>
            <div class="bg-base-200 rounded px-2 py-1.5">
              <span class="text-base-content/50">Days Held</span>
              <div class="font-mono">{{ data()!.holdingDays }}</div>
            </div>
          </div>

          <!-- Chart -->
          <div #chartContainer class="w-full rounded" style="height: 380px;"></div>

          <!-- Three-column info panel -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-3 mt-3">

            <!-- Risk / Reward Card -->
            <div class="bg-base-200 rounded-lg p-3">
              <h4 class="text-xs font-semibold text-base-content/60 uppercase mb-2">Risk / Reward</h4>
              @if (rrRatio() > 0) {
                <!-- R:R bar -->
                <div class="flex h-3 rounded-full overflow-hidden mb-2">
                  <div class="bg-error" [style.flex]="'1'"></div>
                  <div class="bg-success" [style.flex]="rrRatio().toString()"></div>
                </div>
                <div class="text-center font-mono text-sm font-bold mb-2">
                  R:R = 1 : {{ rrRatio() | number:'1.1-1' }}
                </div>
                <div class="space-y-1 text-xs">
                  <div class="flex justify-between">
                    <span class="text-error">Risk</span>
                    <span class="font-mono">
                      \${{ riskAmount() | number:'1.2-2' }}
                      ({{ riskPercent() | number:'1.1-1' }}%)
                    </span>
                  </div>
                  <div class="flex justify-between">
                    <span class="text-success">Reward</span>
                    <span class="font-mono">
                      \${{ rewardAmount() | number:'1.2-2' }}
                      ({{ rewardPercent() | number:'1.1-1' }}%)
                    </span>
                  </div>
                  <div class="divider my-1"></div>
                  <div class="flex justify-between text-base-content/50">
                    <span>SL</span>
                    <span class="font-mono">\${{ data()!.stopLossPrice | number:'1.2-2' }}</span>
                  </div>
                  <div class="flex justify-between text-base-content/50">
                    <span>TP</span>
                    <span class="font-mono">\${{ data()!.takeProfitPrice | number:'1.2-2' }}</span>
                  </div>
                </div>
              } @else {
                <div class="text-xs text-base-content/40">No SL/TP data</div>
              }
            </div>

            <!-- Why This Trade? Card -->
            <div class="bg-base-200 rounded-lg p-3">
              <h4 class="text-xs font-semibold text-base-content/60 uppercase mb-2">Why This Trade?</h4>
              @if (reasoning()) {
                <!-- Conditions fired -->
                <div class="flex flex-wrap gap-1 mb-3">
                  @for (cond of reasoning()!.conditionsFired; track cond) {
                    <span class="badge badge-xs badge-outline badge-success">{{ cond }}</span>
                  }
                </div>
                <!-- Factor contributions -->
                <div class="space-y-1.5">
                  @for (factor of factorEntries(); track factor.key) {
                    <div class="flex items-center gap-2 text-xs">
                      <span class="w-12 text-right text-base-content/60 shrink-0 truncate" [title]="factor.key">{{ factor.key }}</span>
                      <div class="flex-1 bg-base-300 rounded-full h-2 overflow-hidden">
                        <div class="bg-info h-full rounded-full" [style.width.%]="factor.value * 100"></div>
                      </div>
                      <span class="font-mono text-base-content/50 w-6 text-right">{{ factor.value | number:'1.1-1' }}</span>
                    </div>
                  }
                </div>
                <!-- Composite score -->
                <div class="mt-3">
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-base-content/60">Composite Score</span>
                    <span class="font-mono">{{ reasoning()!.compositeScore | number:'1.0-0' }}</span>
                  </div>
                  <progress class="progress progress-info w-full" [value]="reasoning()!.compositeScore" max="100"></progress>
                </div>
              } @else {
                <div class="text-xs text-base-content/40">No reasoning data available</div>
              }
            </div>

            <!-- Trade Outcome Card -->
            <div class="bg-base-200 rounded-lg p-3">
              <h4 class="text-xs font-semibold text-base-content/60 uppercase mb-2">Outcome</h4>
              <!-- Exit reason -->
              <div class="mb-3">
                <span class="badge badge-sm" [class.badge-error]="data()!.exitReason === 'StopLoss'"
                  [class.badge-success]="data()!.exitReason === 'TakeProfit'"
                  [class.badge-warning]="data()!.exitReason === 'MaxHoldingDays'"
                  [class.badge-info]="data()!.exitReason === 'ExitSignal'">
                  {{ exitReasonLabel() }}
                </span>
              </div>
              <!-- Regime confidence -->
              @if (reasoning()) {
                <div class="mb-3">
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-base-content/60">Regime Confidence</span>
                    <span class="font-mono">{{ reasoning()!.regimeConfidence * 100 | number:'1.0-0' }}%</span>
                  </div>
                  <progress class="progress progress-primary w-full" [value]="reasoning()!.regimeConfidence * 100" max="100"></progress>
                </div>
              }
              <div class="space-y-2 text-xs">
                <div class="flex justify-between">
                  <span class="text-base-content/60">Holding Period</span>
                  <span class="font-mono">{{ data()!.holdingDays }} days</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-base-content/60">Commission</span>
                  <span class="font-mono">\${{ data()!.commission | number:'1.2-2' }}</span>
                </div>
                <div class="divider my-1"></div>
                <div class="flex justify-between font-semibold">
                  <span>Net P&L</span>
                  <span class="font-mono" [class.text-success]="data()!.pnL > 0" [class.text-error]="data()!.pnL <= 0">
                    {{ data()!.pnL > 0 ? '+' : '' }}\${{ data()!.pnL | number:'1.2-2' }}
                  </span>
                </div>
              </div>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class TradeChartComponent implements AfterViewInit, OnDestroy, OnChanges {
  @Input() backtestRunId!: string;
  @Input() tradeIndex!: number;
  @Output() close = new EventEmitter<void>();

  @ViewChild('chartContainer') chartEl!: ElementRef<HTMLDivElement>;

  private api = inject(ApiService);
  private chartApi: IChartApi | null = null;
  private markersPlugin: ISeriesMarkersPluginApi<Time> | null = null;

  loading = signal(true);
  error = signal('');
  data = signal<any>(null);
  reasoning = signal<TradeReasoning | null>(null);

  // Computed risk/reward values
  rrRatio = signal(0);
  riskAmount = signal(0);
  rewardAmount = signal(0);
  riskPercent = signal(0);
  rewardPercent = signal(0);
  factorEntries = signal<{ key: string; value: number }[]>([]);

  ngOnChanges(): void {
    this.loadData();
  }

  ngAfterViewInit(): void {
    if (this.data()) {
      setTimeout(() => this.renderChart(), 0);
    }
  }

  ngOnDestroy(): void {
    this.chartApi?.remove();
  }

  exitReasonLabel(): string {
    const d = this.data();
    if (!d) return '';
    switch (d.exitReason) {
      case 'StopLoss': return 'Hit stop loss at $' + (d.stopLossPrice?.toFixed(2) ?? '?');
      case 'TakeProfit': return 'Take profit at $' + (d.takeProfitPrice?.toFixed(2) ?? '?');
      case 'ExitSignal': return 'Exit conditions triggered';
      case 'MaxHoldingDays': return 'Max holding (' + d.holdingDays + ' days)';
      default: return d.exitReason;
    }
  }

  private loadData(): void {
    this.loading.set(true);
    this.error.set('');

    this.api.getTradeChartData(this.backtestRunId, this.tradeIndex).subscribe({
      next: (res) => {
        this.data.set(res);
        this.parseReasoning(res);
        this.computeRiskReward(res);
        this.loading.set(false);
        setTimeout(() => this.renderChart(), 0);
      },
      error: (err) => {
        this.error.set(err.error?.detail ?? 'Failed to load trade chart data');
        this.loading.set(false);
      },
    });
  }

  private parseReasoning(d: any): void {
    if (!d.reasoningJson) {
      this.reasoning.set(null);
      this.factorEntries.set([]);
      return;
    }
    try {
      const r: TradeReasoning = JSON.parse(d.reasoningJson);
      this.reasoning.set(r);
      this.factorEntries.set(
        Object.entries(r.factorContributions ?? {})
          .map(([key, value]) => ({ key, value }))
          .sort((a, b) => b.value - a.value)
      );
    } catch {
      this.reasoning.set(null);
      this.factorEntries.set([]);
    }
  }

  private computeRiskReward(d: any): void {
    const sl = d.stopLossPrice ?? 0;
    const tp = d.takeProfitPrice ?? 0;
    const entry = d.entryPrice ?? 0;
    if (sl > 0 && tp > 0 && entry > 0) {
      const risk = entry - sl;
      const reward = tp - entry;
      this.riskAmount.set(Math.abs(risk));
      this.rewardAmount.set(Math.abs(reward));
      this.riskPercent.set(entry > 0 ? Math.abs(risk / entry) * 100 : 0);
      this.rewardPercent.set(entry > 0 ? Math.abs(reward / entry) * 100 : 0);
      this.rrRatio.set(risk > 0 ? reward / risk : 0);
    } else {
      this.rrRatio.set(0);
      this.riskAmount.set(0);
      this.rewardAmount.set(0);
      this.riskPercent.set(0);
      this.rewardPercent.set(0);
    }
  }

  private renderChart(): void {
    const container = this.chartEl?.nativeElement;
    if (!container || !this.data()) return;

    this.chartApi?.remove();

    const d = this.data();
    const candles = d.candles ?? [];
    if (candles.length === 0) return;

    const chart = createChart(container, {
      layout: { background: { color: 'transparent' }, textColor: '#a6adbb' },
      grid: { vertLines: { color: '#2a2e37' }, horzLines: { color: '#2a2e37' } },
      crosshair: { mode: 0 },
      rightPriceScale: { borderColor: '#2a2e37' },
      timeScale: { borderColor: '#2a2e37' },
      width: container.clientWidth,
      height: 380,
    });
    this.chartApi = chart;

    // Compute price range from candles + entry/exit for auto-scale
    const allPrices = candles.flatMap((c: any) => [c.high, c.low]);
    allPrices.push(d.entryPrice, d.exitPrice);
    // Include SL/TP only if they fall within a reasonable range of the candle data
    const candleMin = Math.min(...candles.map((c: any) => c.low));
    const candleMax = Math.max(...candles.map((c: any) => c.high));
    const candleRange = candleMax - candleMin;
    const clampMargin = candleRange * 0.3; // allow 30% beyond candle range
    if (d.stopLossPrice > 0 && d.stopLossPrice >= candleMin - clampMargin) {
      allPrices.push(d.stopLossPrice);
    }
    if (d.takeProfitPrice > 0 && d.takeProfitPrice <= candleMax + clampMargin) {
      allPrices.push(d.takeProfitPrice);
    }
    const scaleMin = Math.min(...allPrices);
    const scaleMax = Math.max(...allPrices);
    const margin = (scaleMax - scaleMin) * 0.05;

    // Candlestick series
    const candleSeries = chart.addSeries(CandlestickSeries, {
      upColor: '#00c853',
      downColor: '#f87272',
      borderUpColor: '#00c853',
      borderDownColor: '#f87272',
      wickUpColor: '#00c853',
      wickDownColor: '#f87272',
      autoscaleInfoProvider: () => ({
        priceRange: { minValue: scaleMin - margin, maxValue: scaleMax + margin },
      }),
    });

    // Color candles by close vs previous close (TradingView style)
    // This avoids the Yahoo Finance adjusted-close vs unadjusted-open issue
    const candleData = candles.map((c: any, i: number) => {
      const prevClose = i > 0 ? candles[i - 1].close : c.open;
      const isUp = c.close >= prevClose;
      return {
        time: c.date,
        open: c.open,
        high: c.high,
        low: c.low,
        close: c.close,
        color: isUp ? '#00c853' : '#f87272',
        borderColor: isUp ? '#00c853' : '#f87272',
        wickColor: isUp ? '#00c853' : '#f87272',
      };
    });
    candleSeries.setData(candleData);

    // Volume histogram
    const volumeSeries = chart.addSeries(HistogramSeries, {
      priceFormat: { type: 'volume' },
      priceScaleId: 'volume',
    });
    chart.priceScale('volume').applyOptions({
      scaleMargins: { top: 0.85, bottom: 0 },
    });
    volumeSeries.setData(candles.map((c: any, i: number) => {
      const prevClose = i > 0 ? candles[i - 1].close : c.open;
      return {
        time: c.date,
        value: c.volume,
        color: c.close >= prevClose ? 'rgba(0,200,83,0.3)' : 'rgba(248,114,114,0.3)',
      };
    }));

    const entryDate = d.entryDate?.substring(0, 10);
    const exitDate = d.exitDate?.substring(0, 10);

    // TP zone (green shaded band: entry → TP)
    if (d.takeProfitPrice > 0) {
      candleSeries.attachPrimitive(
        this.createBandPrimitive(d.entryPrice, d.takeProfitPrice, 'rgba(0,200,83,0.12)')
      );
      candleSeries.createPriceLine({
        price: d.takeProfitPrice,
        color: '#00c853',
        lineWidth: 1,
        lineStyle: 2,
        axisLabelVisible: true,
        title: 'TP',
      });
    }

    // SL zone (red shaded band: SL → entry)
    if (d.stopLossPrice > 0) {
      candleSeries.attachPrimitive(
        this.createBandPrimitive(d.stopLossPrice, d.entryPrice, 'rgba(248,114,114,0.12)')
      );
      candleSeries.createPriceLine({
        price: d.stopLossPrice,
        color: '#f87272',
        lineWidth: 1,
        lineStyle: 2,
        axisLabelVisible: true,
        title: 'SL',
      });
    }

    // Entry price line (thin, subtle)
    candleSeries.createPriceLine({
      price: d.entryPrice,
      color: '#3b82f6',
      lineWidth: 1,
      lineStyle: 2,
      axisLabelVisible: true,
      title: 'Entry',
    });

    // Markers for entry and exit
    const markers: any[] = [];
    if (entryDate) {
      markers.push({
        time: entryDate,
        position: 'belowBar',
        color: '#3b82f6',
        shape: 'arrowUp',
        size: 2,
        text: 'BUY',
      });
    }
    if (exitDate) {
      markers.push({
        time: exitDate,
        position: 'aboveBar',
        color: d.pnL >= 0 ? '#00c853' : '#f87272',
        shape: 'arrowDown',
        size: 2,
        text: 'SELL',
      });
    }
    if (markers.length > 0) {
      markers.sort((a: any, b: any) => a.time.localeCompare(b.time));
      this.markersPlugin = createSeriesMarkers(candleSeries, markers);
    }

    chart.timeScale().fitContent();

    // Resize observer
    const ro = new ResizeObserver(() => {
      chart.applyOptions({ width: container.clientWidth });
    });
    ro.observe(container);
  }

  private createBandPrimitive(minPrice: number, maxPrice: number, color: string): any {
    let seriesRef: any = null;
    return {
      attached({ series }: any) { seriesRef = series; },
      detached() { seriesRef = null; },
      updateAllViews() {},
      paneViews() {
        return [{
          zOrder: 'bottom',
          renderer() {
            return {
              draw(target: any) {
                target.useBitmapCoordinateSpace((scope: any) => {
                  if (!seriesRef) return;
                  const y1 = seriesRef.priceToCoordinate(maxPrice);
                  const y2 = seriesRef.priceToCoordinate(minPrice);
                  if (y1 === null || y2 === null) return;
                  const ctx = scope.context;
                  const top = Math.round(Math.min(y1, y2) * scope.verticalPixelRatio);
                  const bottom = Math.round(Math.max(y1, y2) * scope.verticalPixelRatio);
                  ctx.fillStyle = color;
                  ctx.fillRect(0, top, scope.bitmapSize.width, bottom - top);
                });
              },
            };
          },
        }];
      },
    };
  }
}
