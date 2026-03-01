import {
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  AfterViewInit,
  SimpleChanges,
  ViewChild,
} from '@angular/core';
import {
  createChart,
  LineSeries,
  HistogramSeries,
  IChartApi,
  ISeriesApi,
  HistogramData,
  ColorType,
  Time,
  SeriesType,
} from 'lightweight-charts';
import { MacdPoint, RsiPoint, StochasticPoint } from '../models/chart.models';

export type IndicatorType = 'rsi' | 'macd' | 'stochastic';

@Component({
  selector: 'app-indicator-panel',
  standalone: true,
  template: `
    <div class="w-full">
      <div class="text-xs text-base-content/50 font-mono mb-1 px-1 uppercase">{{ label }}</div>
      <div #panelContainer class="w-full"></div>
    </div>
  `,
})
export class IndicatorPanelComponent implements AfterViewInit, OnChanges, OnDestroy {
  @ViewChild('panelContainer', { static: true }) panelContainer!: ElementRef<HTMLDivElement>;

  @Input() type: IndicatorType = 'rsi';
  @Input() rsiData: RsiPoint[] = [];
  @Input() macdData: MacdPoint[] = [];
  @Input() stochasticData: StochasticPoint[] = [];
  @Input() height = 120;

  private chart: IChartApi | null = null;
  private series: ISeriesApi<SeriesType>[] = [];
  private resizeObserver: ResizeObserver | null = null;

  get label(): string {
    return this.type.toUpperCase();
  }

  ngAfterViewInit(): void {
    this.createChart();
    this.updateData();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.chart) return;
    this.updateData();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.chart?.remove();
    this.chart = null;
  }

  private createChart(): void {
    const container = this.panelContainer.nativeElement;

    this.chart = createChart(container, {
      width: container.clientWidth,
      height: this.height,
      layout: {
        background: { type: ColorType.Solid, color: 'transparent' },
        textColor: '#a6adba',
        fontFamily: 'ui-monospace, monospace',
        fontSize: 10,
      },
      grid: {
        vertLines: { color: 'rgba(255,255,255,0.03)' },
        horzLines: { color: 'rgba(255,255,255,0.03)' },
      },
      rightPriceScale: {
        borderColor: 'rgba(255,255,255,0.1)',
      },
      timeScale: {
        visible: false,
      },
      crosshair: {
        horzLine: { visible: false },
      },
    });

    this.resizeObserver = new ResizeObserver(() => {
      if (this.chart) {
        this.chart.resize(container.clientWidth, this.height);
      }
    });
    this.resizeObserver.observe(container);
  }

  private updateData(): void {
    if (!this.chart) return;

    // Remove old series
    for (const s of this.series) {
      this.chart.removeSeries(s);
    }
    this.series = [];

    switch (this.type) {
      case 'rsi':
        this.renderRsi();
        break;
      case 'macd':
        this.renderMacd();
        break;
      case 'stochastic':
        this.renderStochastic();
        break;
    }
  }

  private addLine(opts: { color: string; lineWidth?: 1 | 2 | 3 | 4; lineStyle?: number; lastValueVisible?: boolean }): ISeriesApi<'Line'> {
    const s = this.chart!.addSeries(LineSeries, {
      color: opts.color,
      lineWidth: opts.lineWidth ?? 1,
      lineStyle: opts.lineStyle,
      priceLineVisible: false,
      lastValueVisible: opts.lastValueVisible ?? false,
      crosshairMarkerVisible: false,
    });
    this.series.push(s);
    return s;
  }

  private renderRsi(): void {
    if (this.rsiData.length === 0) return;

    // Overbought line (70)
    const ob = this.addLine({ color: 'rgba(239,68,68,0.3)', lineStyle: 2 });
    ob.setData(this.rsiData.map((d) => ({ time: this.toTime(d.time), value: 70 })));

    // Oversold line (30)
    const os = this.addLine({ color: 'rgba(34,197,94,0.3)', lineStyle: 2 });
    os.setData(this.rsiData.map((d) => ({ time: this.toTime(d.time), value: 30 })));

    // RSI line
    const rsi = this.addLine({ color: '#a78bfa', lineWidth: 2, lastValueVisible: true });
    rsi.setData(
      this.rsiData.filter((d) => d.value !== 0).map((d) => ({ time: this.toTime(d.time), value: d.value })),
    );
  }

  private renderMacd(): void {
    if (this.macdData.length === 0) return;

    // MACD histogram
    const hist = this.chart!.addSeries(HistogramSeries, {
      priceLineVisible: false,
      lastValueVisible: false,
    });
    hist.setData(
      this.macdData
        .filter((d) => d.histogram !== 0)
        .map((d) => ({
          time: this.toTime(d.time),
          value: d.histogram,
          color: d.histogram >= 0 ? 'rgba(34,197,94,0.5)' : 'rgba(239,68,68,0.5)',
        })) as HistogramData<Time>[],
    );
    this.series.push(hist);

    // MACD line
    const macdLine = this.addLine({ color: '#3b82f6' });
    macdLine.setData(
      this.macdData.filter((d) => d.macd !== 0).map((d) => ({ time: this.toTime(d.time), value: d.macd })),
    );

    // Signal line
    const signalLine = this.addLine({ color: '#f97316' });
    signalLine.setData(
      this.macdData.filter((d) => d.signal !== 0).map((d) => ({ time: this.toTime(d.time), value: d.signal })),
    );
  }

  private renderStochastic(): void {
    if (this.stochasticData.length === 0) return;

    // OB/OS lines
    const ob = this.addLine({ color: 'rgba(239,68,68,0.3)', lineStyle: 2 });
    ob.setData(this.stochasticData.map((d) => ({ time: this.toTime(d.time), value: 80 })));

    const os = this.addLine({ color: 'rgba(34,197,94,0.3)', lineStyle: 2 });
    os.setData(this.stochasticData.map((d) => ({ time: this.toTime(d.time), value: 20 })));

    // %K line
    const kLine = this.addLine({ color: '#3b82f6', lineWidth: 2, lastValueVisible: true });
    kLine.setData(
      this.stochasticData.filter((d) => d.k !== 0).map((d) => ({ time: this.toTime(d.time), value: d.k })),
    );

    // %D line
    const dLine = this.addLine({ color: '#f97316', lastValueVisible: true });
    dLine.setData(
      this.stochasticData.filter((d) => d.d !== 0).map((d) => ({ time: this.toTime(d.time), value: d.d })),
    );
  }

  private toTime(timestamp: string): Time {
    return timestamp.substring(0, 10) as Time;
  }
}
