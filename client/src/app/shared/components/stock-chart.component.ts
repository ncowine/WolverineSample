import { Component, Input, OnChanges, SimpleChanges, signal, computed } from '@angular/core';
import { PriceChartComponent } from './price-chart.component';
import { IndicatorPanelComponent } from './indicator-panel.component';
import {
  CandleDto,
  ChartTimeframe,
  IndicatorOverlay,
  IndicatorPoint,
  MacdPoint,
  RsiPoint,
  SignalMarker,
  StochasticPoint,
} from '../models/chart.models';

@Component({
  selector: 'app-stock-chart',
  standalone: true,
  imports: [PriceChartComponent, IndicatorPanelComponent],
  template: `
    <div class="flex flex-col gap-1">
      <!-- Timeframe tabs -->
      <div class="flex items-center gap-2 mb-2">
        @if (symbol) {
          <span class="font-mono font-bold text-lg">{{ symbol }}</span>
        }
        <div class="tabs tabs-box tabs-xs ml-auto">
          @for (tf of timeframes; track tf) {
            <button
              class="tab"
              [class.tab-active]="activeTimeframe() === tf"
              (click)="onTimeframeChange(tf)"
            >
              {{ tf }}
            </button>
          }
        </div>
      </div>

      <!-- Overlay legend -->
      @if (activeOverlays().length > 0) {
        <div class="flex gap-3 text-xs font-mono px-1 mb-1">
          @for (o of activeOverlays(); track o.name) {
            <span [style.color]="o.color">{{ o.name }}</span>
          }
        </div>
      }

      <!-- Main price chart -->
      <app-price-chart
        [candles]="activeCandles()"
        [overlays]="activeOverlays()"
        [markers]="activeMarkers()"
        [height]="400"
      />

      <!-- Indicator panels -->
      @if (activeRsi().length > 0) {
        <app-indicator-panel type="rsi" [rsiData]="activeRsi()" [height]="120" />
      }
      @if (activeMacd().length > 0) {
        <app-indicator-panel type="macd" [macdData]="activeMacd()" [height]="120" />
      }
      @if (activeStochastic().length > 0) {
        <app-indicator-panel type="stochastic" [stochasticData]="activeStochastic()" [height]="120" />
      }
    </div>
  `,
})
export class StockChartComponent implements OnChanges {
  @Input() symbol = '';

  /** Candle data per timeframe */
  @Input() dailyCandles: CandleDto[] = [];
  @Input() weeklyCandles: CandleDto[] = [];
  @Input() monthlyCandles: CandleDto[] = [];

  /** Indicator overlays per timeframe */
  @Input() dailyOverlays: IndicatorOverlay[] = [];
  @Input() weeklyOverlays: IndicatorOverlay[] = [];
  @Input() monthlyOverlays: IndicatorOverlay[] = [];

  /** Indicator panel data (computed from daily only by default) */
  @Input() rsiData: RsiPoint[] = [];
  @Input() macdData: MacdPoint[] = [];
  @Input() stochasticData: StochasticPoint[] = [];

  /** Signal markers per timeframe */
  @Input() dailyMarkers: SignalMarker[] = [];
  @Input() weeklyMarkers: SignalMarker[] = [];
  @Input() monthlyMarkers: SignalMarker[] = [];

  /** Available timeframe tabs — pass subset to hide some */
  @Input() timeframes: ChartTimeframe[] = ['Daily', 'Weekly', 'Monthly'];

  /** Callback when timeframe changes */
  @Input() onTimeframeChanged?: (tf: ChartTimeframe) => void;

  activeTimeframe = signal<ChartTimeframe>('Daily');

  // Candle data keyed by timeframe
  private candleMap = signal<Record<ChartTimeframe, CandleDto[]>>({
    Daily: [],
    Weekly: [],
    Monthly: [],
  });
  private overlayMap = signal<Record<ChartTimeframe, IndicatorOverlay[]>>({
    Daily: [],
    Weekly: [],
    Monthly: [],
  });
  private markerMap = signal<Record<ChartTimeframe, SignalMarker[]>>({
    Daily: [],
    Weekly: [],
    Monthly: [],
  });

  activeCandles = computed(() => this.candleMap()[this.activeTimeframe()]);
  activeOverlays = computed(() => this.overlayMap()[this.activeTimeframe()]);
  activeMarkers = computed(() => this.markerMap()[this.activeTimeframe()]);

  // Indicator panels show for all timeframes (data is typically daily)
  activeRsi = signal<RsiPoint[]>([]);
  activeMacd = signal<MacdPoint[]>([]);
  activeStochastic = signal<StochasticPoint[]>([]);

  ngOnChanges(changes: SimpleChanges): void {
    this.candleMap.set({
      Daily: this.dailyCandles,
      Weekly: this.weeklyCandles,
      Monthly: this.monthlyCandles,
    });
    this.overlayMap.set({
      Daily: this.dailyOverlays,
      Weekly: this.weeklyOverlays,
      Monthly: this.monthlyOverlays,
    });
    this.markerMap.set({
      Daily: this.dailyMarkers,
      Weekly: this.weeklyMarkers,
      Monthly: this.monthlyMarkers,
    });
    this.activeRsi.set(this.rsiData);
    this.activeMacd.set(this.macdData);
    this.activeStochastic.set(this.stochasticData);
  }

  onTimeframeChange(tf: ChartTimeframe): void {
    this.activeTimeframe.set(tf);
    this.onTimeframeChanged?.(tf);
  }
}
