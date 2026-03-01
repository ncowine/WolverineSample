import {
  Component, inject, signal, OnInit, OnDestroy,
  AfterViewInit, ElementRef, ViewChild,
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import {
  createChart, CandlestickSeries, LineSeries,
  type IChartApi,
} from 'lightweight-charts';

@Component({
  selector: 'app-screener-detail',
  standalone: true,
  imports: [RouterLink, DecimalPipe],
  template: `
    <div class="max-w-5xl">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-4">
        <a routerLink="/screener" class="btn btn-ghost btn-sm">&larr;</a>
        <h2 class="text-2xl font-bold font-mono">{{ symbolParam }}</h2>
        @if (sig()) {
          <span class="badge font-bold text-lg"
                [class.badge-success]="sig()!.grade === 'A'"
                [class.badge-info]="sig()!.grade === 'B'"
                [class.badge-warning]="sig()!.grade === 'C'"
                [class.badge-neutral]="sig()!.grade === 'D'"
                [class.badge-error]="sig()!.grade === 'F'">
            {{ sig()!.grade }}
          </span>
          <span class="text-sm text-base-content/60">Score: {{ sig()!.score | number:'1.0-0' }}/100</span>
          <span class="badge badge-sm badge-outline"
                [class.text-success]="sig()!.direction === 'Long'"
                [class.text-error]="sig()!.direction === 'Short'">
            {{ sig()!.direction }}
          </span>
        }
      </div>

      @if (loading()) {
        <div class="flex items-center gap-3 p-8">
          <span class="loading loading-spinner loading-md"></span>
          <span class="text-sm">Loading signal data...</span>
        </div>
      } @else if (error()) {
        <div class="alert alert-error text-sm">{{ error() }}</div>
      } @else if (sig()) {

        <!-- Price levels + R:R -->
        <div class="grid grid-cols-2 md:grid-cols-5 gap-3 mb-4">
          <div class="bg-base-200 rounded-lg p-3 text-center">
            <div class="text-[10px] text-base-content/50 uppercase">Entry</div>
            <div class="text-lg font-bold font-mono">{{ sig()!.entryPrice | number:'1.2-2' }}</div>
          </div>
          <div class="bg-base-200 rounded-lg p-3 text-center">
            <div class="text-[10px] text-base-content/50 uppercase">Stop</div>
            <div class="text-lg font-bold font-mono text-error">{{ sig()!.stopPrice | number:'1.2-2' }}</div>
          </div>
          <div class="bg-base-200 rounded-lg p-3 text-center">
            <div class="text-[10px] text-base-content/50 uppercase">Target</div>
            <div class="text-lg font-bold font-mono text-success">{{ sig()!.targetPrice | number:'1.2-2' }}</div>
          </div>
          <div class="bg-base-200 rounded-lg p-3 text-center">
            <div class="text-[10px] text-base-content/50 uppercase">R:R Ratio</div>
            <div class="text-lg font-bold font-mono">{{ sig()!.riskRewardRatio | number:'1.1-1' }}</div>
          </div>
          <div class="bg-base-200 rounded-lg p-3 text-center">
            <div class="text-[10px] text-base-content/50 uppercase">Historical Win</div>
            <div class="text-lg font-bold font-mono">
              @if (sig()!.historicalWinRate !== null && sig()!.historicalWinRate !== undefined) {
                {{ sig()!.historicalWinRate | number:'1.0-0' }}%
              } @else {
                <span class="text-base-content/30">—</span>
              }
            </div>
          </div>
        </div>

        <!-- Price Chart with entry/stop/target lines -->
        <div class="card bg-base-200 mb-4">
          <div class="card-body p-4">
            <div class="flex items-center justify-between mb-2">
              <h3 class="text-sm font-semibold">Price Chart</h3>
              <div class="flex gap-3 text-xs text-base-content/50">
                <span class="flex items-center gap-1"><span class="w-3 h-0.5 bg-info inline-block"></span> Entry</span>
                <span class="flex items-center gap-1"><span class="w-3 h-0.5 bg-error inline-block"></span> Stop</span>
                <span class="flex items-center gap-1"><span class="w-3 h-0.5 bg-success inline-block"></span> Target</span>
              </div>
            </div>
            <div #priceChart class="w-full" style="height: 350px;"></div>
          </div>
        </div>

        <!-- Confirmation Breakdown -->
        <div class="card bg-base-200 mb-4">
          <div class="card-body p-4 gap-3">
            <h3 class="text-sm font-semibold">Score Breakdown</h3>

            @if (sig()!.breakdown && sig()!.breakdown.length > 0) {
              <div class="space-y-3">
                @for (b of sig()!.breakdown; track b.factor) {
                  <div>
                    <div class="flex items-center justify-between text-xs mb-1">
                      <span class="font-medium w-28">{{ b.factor }}</span>
                      <span class="text-base-content/50">{{ b.reason }}</span>
                      <span class="font-mono w-16 text-right">{{ b.weightedScore | number:'1.1-1' }}</span>
                    </div>
                    <div class="flex gap-1 items-center">
                      <div class="flex-1 bg-base-300 rounded-full h-2.5 overflow-hidden">
                        <div class="h-full rounded-full transition-all"
                             [style.width.%]="b.rawScore"
                             [class.bg-success]="b.rawScore >= 70"
                             [class.bg-warning]="b.rawScore >= 40 && b.rawScore < 70"
                             [class.bg-error]="b.rawScore < 40">
                        </div>
                      </div>
                      <span class="text-[10px] font-mono w-10 text-right text-base-content/50">{{ b.rawScore | number:'1.0-0' }} &times; {{ b.weight | number:'1.0-0' }}%</span>
                    </div>
                  </div>
                }
              </div>

              <!-- Total -->
              <div class="flex items-center justify-between pt-2 border-t border-base-300">
                <span class="text-sm font-bold">Total Score</span>
                <span class="text-lg font-bold font-mono"
                      [class.text-success]="sig()!.score >= 75"
                      [class.text-warning]="sig()!.score >= 40 && sig()!.score < 75"
                      [class.text-error]="sig()!.score < 40">
                  {{ sig()!.score | number:'1.0-0' }}
                </span>
              </div>
            } @else {
              <p class="text-sm text-base-content/50">No breakdown data available.</p>
            }
          </div>
        </div>

        <!-- Paper Trade Button -->
        <div class="flex justify-end">
          <button class="btn btn-primary btn-sm" (click)="paperTrade()">
            Paper Trade This Signal
          </button>
        </div>
      }
    </div>
  `,
})
export class ScreenerDetailComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(ApiService);

  @ViewChild('priceChart') priceChartEl!: ElementRef<HTMLDivElement>;

  symbolParam = '';
  loading = signal(true);
  error = signal('');
  sig = signal<any>(null);
  candles = signal<any[]>([]);

  private chart: IChartApi | null = null;
  private resizeObs: ResizeObserver | null = null;

  ngOnInit(): void {
    this.symbolParam = this.route.snapshot.paramMap.get('symbol') ?? '';
    if (!this.symbolParam) { this.loading.set(false); this.error.set('No symbol'); return; }

    // Load signal data and stock history in parallel
    this.api.getScreenerSignal(this.symbolParam).subscribe({
      next: (res) => {
        this.sig.set(res);
        this.loading.set(false);
        this.loadChartData();
      },
      error: (err) => {
        this.error.set(err.error?.detail ?? 'Signal not found');
        this.loading.set(false);
      },
    });
  }

  ngOnDestroy(): void {
    this.resizeObs?.disconnect();
    this.chart?.remove();
  }

  private loadChartData(): void {
    this.api.getStockHistory(this.symbolParam, { interval: 'Daily', limit: 120 }).subscribe({
      next: (data) => {
        this.candles.set(Array.isArray(data) ? data : data.candles ?? []);
        setTimeout(() => this.renderChart(), 0);
      },
      error: () => {}, // Chart is optional enhancement
    });
  }

  private renderChart(): void {
    if (!this.priceChartEl?.nativeElement || this.candles().length === 0) return;
    this.chart?.remove();

    const container = this.priceChartEl.nativeElement;
    const chart = createChart(container, {
      layout: { background: { color: 'transparent' }, textColor: '#a6adbb' },
      grid: { vertLines: { color: '#2a2e37' }, horzLines: { color: '#2a2e37' } },
      crosshair: { mode: 0 },
      rightPriceScale: { borderColor: '#2a2e37' },
      timeScale: { borderColor: '#2a2e37' },
      width: container.clientWidth,
      height: 350,
    });
    this.chart = chart;

    // Candlestick series
    const candleSeries = chart.addSeries(CandlestickSeries, {
      upColor: '#00c853', downColor: '#f87272',
      borderUpColor: '#00c853', borderDownColor: '#f87272',
      wickUpColor: '#00c853', wickDownColor: '#f87272',
    });

    const candleData = this.candles()
      .map((c: any) => ({
        time: (c.timestamp || c.date || '').substring(0, 10),
        open: c.open, high: c.high, low: c.low, close: c.close,
      }))
      .filter((c: any) => c.time)
      .sort((a: any, b: any) => a.time.localeCompare(b.time));

    candleSeries.setData(candleData);

    // Price lines for entry, stop, target
    const s = this.sig();
    if (s) {
      candleSeries.createPriceLine({
        price: s.entryPrice, color: '#3abff8', lineWidth: 1, lineStyle: 2,
        axisLabelVisible: true, title: 'Entry',
      });
      candleSeries.createPriceLine({
        price: s.stopPrice, color: '#f87272', lineWidth: 1, lineStyle: 2,
        axisLabelVisible: true, title: 'Stop',
      });
      candleSeries.createPriceLine({
        price: s.targetPrice, color: '#00c853', lineWidth: 1, lineStyle: 2,
        axisLabelVisible: true, title: 'Target',
      });
    }

    chart.timeScale().fitContent();

    this.resizeObs?.disconnect();
    this.resizeObs = new ResizeObserver(() => {
      chart.applyOptions({ width: container.clientWidth });
    });
    this.resizeObs.observe(container);
  }

  paperTrade(): void {
    const s = this.sig();
    if (!s) return;
    // Navigate to orders page with pre-filled params
    this.router.navigate(['/orders'], {
      queryParams: {
        symbol: s.symbol,
        side: s.direction === 'Long' ? 'Buy' : 'Sell',
        price: s.entryPrice,
        stopPrice: s.stopPrice,
        targetPrice: s.targetPrice,
      },
    });
  }
}
