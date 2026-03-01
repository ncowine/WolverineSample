/** OHLCV candle from the backend */
export interface CandleDto {
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  timestamp: string;
  interval: string;
}

/** Indicator overlay data point */
export interface IndicatorPoint {
  time: string;
  value: number;
}

/** Signal marker for buy/sell signals on chart */
export interface SignalMarker {
  time: string;
  position: 'aboveBar' | 'belowBar';
  color: string;
  shape: 'arrowUp' | 'arrowDown';
  text: string;
}

/** Chart timeframe options */
export type ChartTimeframe = 'Daily' | 'Weekly' | 'Monthly';

/** Indicator overlay configuration */
export interface IndicatorOverlay {
  name: string;
  data: IndicatorPoint[];
  color: string;
  lineWidth?: number;
}

/** RSI data point */
export interface RsiPoint {
  time: string;
  value: number;
}

/** MACD data with histogram */
export interface MacdPoint {
  time: string;
  macd: number;
  signal: number;
  histogram: number;
}

/** Stochastic data */
export interface StochasticPoint {
  time: string;
  k: number;
  d: number;
}
