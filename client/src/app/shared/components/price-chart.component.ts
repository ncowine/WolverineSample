import {
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  AfterViewInit,
} from '@angular/core';
import {
  createChart,
  createSeriesMarkers,
  CandlestickSeries,
  LineSeries,
  HistogramSeries,
  IChartApi,
  ISeriesApi,
  ISeriesMarkersPluginApi,
  CandlestickData,
  HistogramData,
  LineData,
  ColorType,
  CrosshairMode,
  Time,
} from 'lightweight-charts';
import { CandleDto, IndicatorOverlay, SignalMarker } from '../models/chart.models';

@Component({
  selector: 'app-price-chart',
  standalone: true,
  template: `
    <div class="w-full" style="min-height: 400px">
      <div #chartContainer class="w-full h-full"></div>
    </div>
  `,
})
export class PriceChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('chartContainer', { static: true }) chartContainer!: ElementRef<HTMLDivElement>;

  @Input() candles: CandleDto[] = [];
  @Input() overlays: IndicatorOverlay[] = [];
  @Input() markers: SignalMarker[] = [];
  @Input() height = 400;

  private chart: IChartApi | null = null;
  private candleSeries: ISeriesApi<'Candlestick'> | null = null;
  private volumeSeries: ISeriesApi<'Histogram'> | null = null;
  private overlaySeries: ISeriesApi<'Line'>[] = [];
  private markersPlugin: ISeriesMarkersPluginApi<Time> | null = null;
  private resizeObserver: ResizeObserver | null = null;

  ngAfterViewInit(): void {
    this.createChart();
    this.updateData();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.chart) return;

    if (changes['candles'] || changes['overlays'] || changes['markers']) {
      this.updateData();
    }

    if (changes['height']) {
      this.chart.resize(this.chartContainer.nativeElement.clientWidth, this.height);
    }
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.chart?.remove();
    this.chart = null;
  }

  private createChart(): void {
    const container = this.chartContainer.nativeElement;

    this.chart = createChart(container, {
      width: container.clientWidth,
      height: this.height,
      layout: {
        background: { type: ColorType.Solid, color: 'transparent' },
        textColor: '#a6adba',
        fontFamily: 'ui-monospace, monospace',
      },
      grid: {
        vertLines: { color: 'rgba(255,255,255,0.04)' },
        horzLines: { color: 'rgba(255,255,255,0.04)' },
      },
      crosshair: { mode: CrosshairMode.Normal },
      rightPriceScale: {
        borderColor: 'rgba(255,255,255,0.1)',
      },
      timeScale: {
        borderColor: 'rgba(255,255,255,0.1)',
        timeVisible: false,
      },
    });

    // Candlestick series
    this.candleSeries = this.chart.addSeries(CandlestickSeries, {
      upColor: '#22c55e',
      downColor: '#ef4444',
      borderUpColor: '#22c55e',
      borderDownColor: '#ef4444',
      wickUpColor: '#22c55e',
      wickDownColor: '#ef4444',
    });

    // Volume histogram on same pane with overlay
    this.volumeSeries = this.chart.addSeries(HistogramSeries, {
      priceFormat: { type: 'volume' },
      priceScaleId: 'volume',
    });

    this.chart.priceScale('volume').applyOptions({
      scaleMargins: { top: 0.8, bottom: 0 },
    });

    // Responsive resize
    this.resizeObserver = new ResizeObserver(() => {
      if (this.chart) {
        this.chart.resize(container.clientWidth, this.height);
      }
    });
    this.resizeObserver.observe(container);
  }

  private updateData(): void {
    if (!this.chart || !this.candleSeries || !this.volumeSeries) return;

    // Clear old overlay series
    for (const s of this.overlaySeries) {
      this.chart.removeSeries(s);
    }
    this.overlaySeries = [];

    if (this.candles.length === 0) return;

    // Map candle data
    const candleData: CandlestickData<Time>[] = this.candles.map((c) => ({
      time: this.toChartTime(c.timestamp),
      open: c.open,
      high: c.high,
      low: c.low,
      close: c.close,
    }));

    const volumeData: HistogramData<Time>[] = this.candles.map((c) => ({
      time: this.toChartTime(c.timestamp),
      value: c.volume,
      color: c.close >= c.open ? 'rgba(34,197,94,0.3)' : 'rgba(239,68,68,0.3)',
    }));

    this.candleSeries.setData(candleData);
    this.volumeSeries.setData(volumeData);

    // Add indicator overlays (SMA, EMA, Bollinger Bands)
    for (const overlay of this.overlays) {
      const series = this.chart.addSeries(LineSeries, {
        color: overlay.color,
        lineWidth: (overlay.lineWidth ?? 1) as 1 | 2 | 3 | 4,
        priceLineVisible: false,
        lastValueVisible: false,
        crosshairMarkerVisible: false,
      });

      const lineData: LineData<Time>[] = overlay.data
        .filter((d) => d.value !== 0)
        .map((d) => ({
          time: this.toChartTime(d.time),
          value: d.value,
        }));

      series.setData(lineData);
      this.overlaySeries.push(series);
    }

    // Add signal markers via plugin
    if (this.markers.length > 0 && this.candleSeries) {
      if (this.markersPlugin) {
        this.markersPlugin.detach();
      }
      this.markersPlugin = createSeriesMarkers(
        this.candleSeries,
        this.markers.map((m) => ({
          time: this.toChartTime(m.time),
          position: m.position,
          color: m.color,
          shape: m.shape as 'arrowUp' | 'arrowDown',
          text: m.text,
        })),
      );
    }

    this.chart.timeScale().fitContent();
  }

  private toChartTime(timestamp: string): Time {
    // Backend returns ISO date — extract YYYY-MM-DD for daily charts
    return timestamp.substring(0, 10) as Time;
  }
}
