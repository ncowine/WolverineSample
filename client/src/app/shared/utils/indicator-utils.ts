import {
  CandleDto,
  IndicatorOverlay,
  IndicatorPoint,
  MacdPoint,
  RsiPoint,
  StochasticPoint,
} from '../models/chart.models';

/** Compute Simple Moving Average from candle close prices. */
export function computeSma(candles: CandleDto[], period: number, name?: string, color?: string): IndicatorOverlay {
  const data: IndicatorPoint[] = [];
  for (let i = 0; i < candles.length; i++) {
    if (i < period - 1) {
      data.push({ time: candles[i].timestamp, value: 0 });
      continue;
    }
    let sum = 0;
    for (let j = i - period + 1; j <= i; j++) {
      sum += candles[j].close;
    }
    data.push({ time: candles[i].timestamp, value: sum / period });
  }
  return { name: name ?? `SMA ${period}`, data, color: color ?? '#3b82f6' };
}

/** Compute Exponential Moving Average from candle close prices. */
export function computeEma(candles: CandleDto[], period: number, name?: string, color?: string): IndicatorOverlay {
  const data: IndicatorPoint[] = [];
  const multiplier = 2 / (period + 1);
  let ema = 0;

  for (let i = 0; i < candles.length; i++) {
    if (i < period - 1) {
      data.push({ time: candles[i].timestamp, value: 0 });
      continue;
    }
    if (i === period - 1) {
      // Seed with SMA
      let sum = 0;
      for (let j = 0; j < period; j++) sum += candles[j].close;
      ema = sum / period;
    } else {
      ema = (candles[i].close - ema) * multiplier + ema;
    }
    data.push({ time: candles[i].timestamp, value: ema });
  }
  return { name: name ?? `EMA ${period}`, data, color: color ?? '#f59e0b' };
}

/** Compute Bollinger Bands (middle = SMA, upper/lower = SMA +/- 2*stddev). */
export function computeBollingerBands(
  candles: CandleDto[],
  period = 20,
  stdMultiplier = 2,
): { upper: IndicatorOverlay; middle: IndicatorOverlay; lower: IndicatorOverlay } {
  const upper: IndicatorPoint[] = [];
  const middle: IndicatorPoint[] = [];
  const lower: IndicatorPoint[] = [];

  for (let i = 0; i < candles.length; i++) {
    if (i < period - 1) {
      upper.push({ time: candles[i].timestamp, value: 0 });
      middle.push({ time: candles[i].timestamp, value: 0 });
      lower.push({ time: candles[i].timestamp, value: 0 });
      continue;
    }
    const slice = candles.slice(i - period + 1, i + 1).map((c) => c.close);
    const mean = slice.reduce((a, b) => a + b, 0) / period;
    const variance = slice.reduce((a, b) => a + (b - mean) ** 2, 0) / period;
    const std = Math.sqrt(variance);

    middle.push({ time: candles[i].timestamp, value: mean });
    upper.push({ time: candles[i].timestamp, value: mean + stdMultiplier * std });
    lower.push({ time: candles[i].timestamp, value: mean - stdMultiplier * std });
  }

  return {
    upper: { name: 'BB Upper', data: upper, color: 'rgba(148,163,184,0.4)', lineWidth: 1 },
    middle: { name: 'BB Mid', data: middle, color: 'rgba(148,163,184,0.6)', lineWidth: 1 },
    lower: { name: 'BB Lower', data: lower, color: 'rgba(148,163,184,0.4)', lineWidth: 1 },
  };
}

/** Compute RSI from candle close prices. */
export function computeRsi(candles: CandleDto[], period = 14): RsiPoint[] {
  const result: RsiPoint[] = [];
  if (candles.length < period + 1) return result;

  let avgGain = 0;
  let avgLoss = 0;

  // Initial average
  for (let i = 1; i <= period; i++) {
    const change = candles[i].close - candles[i - 1].close;
    if (change > 0) avgGain += change;
    else avgLoss += Math.abs(change);
    result.push({ time: candles[i].timestamp, value: 0 });
  }
  avgGain /= period;
  avgLoss /= period;

  const rs = avgLoss === 0 ? 100 : avgGain / avgLoss;
  result[result.length - 1].value = 100 - 100 / (1 + rs);

  // Smoothed subsequent values
  for (let i = period + 1; i < candles.length; i++) {
    const change = candles[i].close - candles[i - 1].close;
    avgGain = (avgGain * (period - 1) + (change > 0 ? change : 0)) / period;
    avgLoss = (avgLoss * (period - 1) + (change < 0 ? Math.abs(change) : 0)) / period;
    const rsi = avgLoss === 0 ? 100 : 100 - 100 / (1 + avgGain / avgLoss);
    result.push({ time: candles[i].timestamp, value: rsi });
  }

  // Pad front with zeros
  const padded: RsiPoint[] = [];
  for (let i = 0; i < 1; i++) {
    padded.push({ time: candles[i].timestamp, value: 0 });
  }
  return [...padded, ...result];
}

/** Compute MACD (12, 26, 9 default). */
export function computeMacd(candles: CandleDto[], fast = 12, slow = 26, signalPeriod = 9): MacdPoint[] {
  const emaFast = computeEmaValues(candles, fast);
  const emaSlow = computeEmaValues(candles, slow);

  const macdLine: number[] = [];
  for (let i = 0; i < candles.length; i++) {
    if (emaFast[i] === 0 || emaSlow[i] === 0) {
      macdLine.push(0);
    } else {
      macdLine.push(emaFast[i] - emaSlow[i]);
    }
  }

  // Signal line = EMA of MACD
  const signalLine: number[] = new Array(candles.length).fill(0);
  const mult = 2 / (signalPeriod + 1);
  let signalEma = 0;
  let validCount = 0;

  for (let i = 0; i < candles.length; i++) {
    if (macdLine[i] === 0) continue;
    validCount++;
    if (validCount < signalPeriod) {
      signalEma += macdLine[i];
      continue;
    }
    if (validCount === signalPeriod) {
      signalEma = (signalEma + macdLine[i]) / signalPeriod;
    } else {
      signalEma = (macdLine[i] - signalEma) * mult + signalEma;
    }
    signalLine[i] = signalEma;
  }

  return candles.map((c, i) => ({
    time: c.timestamp,
    macd: macdLine[i],
    signal: signalLine[i],
    histogram: macdLine[i] !== 0 && signalLine[i] !== 0 ? macdLine[i] - signalLine[i] : 0,
  }));
}

/** Compute Stochastic Oscillator (%K, %D). */
export function computeStochastic(candles: CandleDto[], kPeriod = 14, dPeriod = 3): StochasticPoint[] {
  const result: StochasticPoint[] = [];

  for (let i = 0; i < candles.length; i++) {
    if (i < kPeriod - 1) {
      result.push({ time: candles[i].timestamp, k: 0, d: 0 });
      continue;
    }
    const slice = candles.slice(i - kPeriod + 1, i + 1);
    const high = Math.max(...slice.map((c) => c.high));
    const low = Math.min(...slice.map((c) => c.low));
    const k = high === low ? 50 : ((candles[i].close - low) / (high - low)) * 100;
    result.push({ time: candles[i].timestamp, k, d: 0 });
  }

  // %D = SMA of %K
  for (let i = kPeriod - 1 + dPeriod - 1; i < result.length; i++) {
    let sum = 0;
    for (let j = i - dPeriod + 1; j <= i; j++) sum += result[j].k;
    result[i].d = sum / dPeriod;
  }

  return result;
}

/** Helper: compute raw EMA values as number array. */
function computeEmaValues(candles: CandleDto[], period: number): number[] {
  const result: number[] = new Array(candles.length).fill(0);
  const multiplier = 2 / (period + 1);
  let ema = 0;

  for (let i = 0; i < candles.length; i++) {
    if (i < period - 1) continue;
    if (i === period - 1) {
      let sum = 0;
      for (let j = 0; j < period; j++) sum += candles[j].close;
      ema = sum / period;
    } else {
      ema = (candles[i].close - ema) * multiplier + ema;
    }
    result[i] = ema;
  }
  return result;
}
