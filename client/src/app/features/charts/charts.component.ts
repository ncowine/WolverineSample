import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { StockChartComponent } from '../../shared/components/stock-chart.component';
import {
  CandleDto,
  ChartTimeframe,
  IndicatorOverlay,
  MacdPoint,
  RsiPoint,
  SignalMarker,
  StochasticPoint,
} from '../../shared/models/chart.models';
import {
  computeSma,
  computeEma,
  computeBollingerBands,
  computeRsi,
  computeMacd,
  computeStochastic,
} from '../../shared/utils/indicator-utils';

@Component({
  selector: 'app-charts',
  standalone: true,
  imports: [FormsModule, StockChartComponent],
  template: `
    <div class="flex flex-col gap-4">
      <!-- Symbol input -->
      <div class="flex items-center gap-3">
        <h2 class="text-2xl font-bold">Charts</h2>
        <div class="join ml-auto">
          <input
            type="text"
            class="input input-sm input-bordered join-item w-32 font-mono uppercase"
            placeholder="Symbol"
            [(ngModel)]="symbolInput"
            (keyup.enter)="loadChart()"
          />
          <button class="btn btn-sm btn-primary join-item" (click)="loadChart()" [disabled]="loading()">
            @if (loading()) {
              <span class="loading loading-spinner loading-xs"></span>
            } @else {
              Load
            }
          </button>
        </div>
      </div>

      @if (error()) {
        <div class="alert alert-error text-sm">{{ error() }}</div>
      }

      @if (dailyCandles().length > 0) {
        <app-stock-chart
          [symbol]="activeSymbol()"
          [dailyCandles]="dailyCandles()"
          [weeklyCandles]="weeklyCandles()"
          [monthlyCandles]="monthlyCandles()"
          [dailyOverlays]="dailyOverlays()"
          [weeklyOverlays]="weeklyOverlays()"
          [monthlyOverlays]="monthlyOverlays()"
          [rsiData]="rsiData()"
          [macdData]="macdData()"
          [stochasticData]="stochasticData()"
          [dailyMarkers]="dailyMarkers()"
          [onTimeframeChanged]="onTimeframeChanged"
        />
      } @else if (!loading()) {
        <div class="card bg-base-200">
          <div class="card-body text-center text-base-content/50">
            <p>Enter a symbol and click Load to view chart data.</p>
            <p class="text-xs mt-1">Requires ingested market data (use Market Data > Ingest first).</p>
          </div>
        </div>
      }
    </div>
  `,
})
export class ChartsComponent {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);

  symbolInput = '';
  activeSymbol = signal('');
  loading = signal(false);
  error = signal('');

  dailyCandles = signal<CandleDto[]>([]);
  weeklyCandles = signal<CandleDto[]>([]);
  monthlyCandles = signal<CandleDto[]>([]);

  dailyOverlays = signal<IndicatorOverlay[]>([]);
  weeklyOverlays = signal<IndicatorOverlay[]>([]);
  monthlyOverlays = signal<IndicatorOverlay[]>([]);

  dailyMarkers = signal<SignalMarker[]>([]);

  rsiData = signal<RsiPoint[]>([]);
  macdData = signal<MacdPoint[]>([]);
  stochasticData = signal<StochasticPoint[]>([]);

  constructor() {
    // Check for symbol in route params
    const paramSymbol = this.route.snapshot.queryParamMap.get('symbol');
    if (paramSymbol) {
      this.symbolInput = paramSymbol;
      this.loadChart();
    }
  }

  onTimeframeChanged = (tf: ChartTimeframe): void => {
    // Could reload indicators for the new timeframe if needed
  };

  loadChart(): void {
    const symbol = this.symbolInput.trim().toUpperCase();
    if (!symbol) return;

    this.loading.set(true);
    this.error.set('');
    this.activeSymbol.set(symbol);

    const now = new Date();
    const twoYearsAgo = new Date(now.getFullYear() - 2, now.getMonth(), now.getDate());
    const startDate = twoYearsAgo.toISOString();
    const endDate = now.toISOString();

    // Load all three timeframes in parallel
    let completed = 0;
    const onComplete = () => {
      completed++;
      if (completed === 3) this.loading.set(false);
    };

    this.loadTimeframe(symbol, startDate, endDate, 'Daily', (candles) => {
      this.dailyCandles.set(candles);
      this.dailyOverlays.set(this.computeOverlays(candles));
      this.rsiData.set(computeRsi(candles));
      this.macdData.set(computeMacd(candles));
      this.stochasticData.set(computeStochastic(candles));
      onComplete();
    });

    this.loadTimeframe(symbol, startDate, endDate, 'Weekly', (candles) => {
      this.weeklyCandles.set(candles);
      this.weeklyOverlays.set(this.computeOverlays(candles));
      onComplete();
    });

    this.loadTimeframe(symbol, startDate, endDate, 'Monthly', (candles) => {
      this.monthlyCandles.set(candles);
      this.monthlyOverlays.set(this.computeOverlays(candles));
      onComplete();
    });
  }

  private loadTimeframe(
    symbol: string,
    startDate: string,
    endDate: string,
    interval: string,
    onSuccess: (candles: CandleDto[]) => void,
  ): void {
    this.api.getStockHistory(symbol, { startDate, endDate, interval }).subscribe({
      next: (candles: CandleDto[]) => onSuccess(candles),
      error: (err: any) => {
        if (interval === 'Daily') {
          this.error.set(`Failed to load ${interval} data for ${symbol}`);
        }
        onSuccess([]);
      },
    });
  }

  private computeOverlays(candles: CandleDto[]): IndicatorOverlay[] {
    if (candles.length < 20) return [];

    const overlays: IndicatorOverlay[] = [];

    // SMA 20 (short-term)
    overlays.push(computeSma(candles, 20, 'SMA 20', '#3b82f6'));

    // EMA 50 (medium-term)
    if (candles.length >= 50) {
      overlays.push(computeEma(candles, 50, 'EMA 50', '#f59e0b'));
    }

    // Bollinger Bands
    const bb = computeBollingerBands(candles);
    overlays.push(bb.upper, bb.middle, bb.lower);

    return overlays;
  }
}
