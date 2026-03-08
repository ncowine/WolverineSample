import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';

@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private http: HttpClient) {}

  // --- Market Data ---
  seedMarketData() {
    return this.http.post('/api/market-data/seed', {});
  }

  getStockPrice(symbol: string) {
    return this.http.get<any>(`/api/market-data/stocks/${symbol}/price`);
  }

  getStockHistory(symbol: string, params?: any) {
    return this.http.get<any>(`/api/market-data/stocks/${symbol}/history`, { params: this.toHttpParams(params) });
  }

  fetchMarketData(symbol: string, from?: string, to?: string) {
    return this.http.post<any>('/api/market-data/fetch', { symbol, from, to });
  }

  ingestMarketData(symbol: string, yearsBack = 5) {
    return this.http.post<any>('/api/market-data/ingest', { symbol, yearsBack });
  }

  // --- Trading: Accounts ---
  createPaperAccount() {
    return this.http.post<any>('/api/trading/accounts/paper', {});
  }

  // --- Trading: Orders ---
  placeOrder(order: any) {
    return this.http.post<any>('/api/trading/orders', order);
  }

  cancelOrder(command: any) {
    return this.http.post<any>('/api/trading/orders/cancel', command);
  }

  getOrders(accountId: string, params?: any) {
    return this.http.get<any>(`/api/trading/orders/${accountId}`, { params: this.toHttpParams(params) });
  }

  // --- Trading: Positions ---
  closePosition(command: any) {
    return this.http.post<any>('/api/trading/positions/close', command);
  }

  getPositions(accountId: string, params?: any) {
    return this.http.get<any>(`/api/trading/positions/${accountId}`, { params: this.toHttpParams(params) });
  }

  // --- Trading: Portfolio ---
  getPortfolio(accountId: string) {
    return this.http.get<any>(`/api/trading/portfolio/${accountId}`);
  }

  // --- DCA Plans ---
  createDcaPlan(plan: any) {
    return this.http.post<any>('/api/trading/dca-plans', plan);
  }

  getDcaPlans(accountId: string) {
    return this.http.get<any>(`/api/trading/dca-plans/${accountId}`);
  }

  pauseDcaPlan(planId: string) {
    return this.http.post<any>(`/api/trading/dca-plans/${planId}/pause`, {});
  }

  resumeDcaPlan(planId: string) {
    return this.http.post<any>(`/api/trading/dca-plans/${planId}/resume`, {});
  }

  cancelDcaPlan(planId: string) {
    return this.http.delete<any>(`/api/trading/dca-plans/${planId}`);
  }

  getDcaExecutions(planId: string, params?: any) {
    return this.http.get<any>(`/api/trading/dca-plans/${planId}/executions`, { params: this.toHttpParams(params) });
  }

  // --- Trade Notes ---
  createNote(note: any) {
    return this.http.post<any>('/api/trading/notes', note);
  }

  getNotes(params?: any) {
    return this.http.get<any>('/api/trading/notes', { params: this.toHttpParams(params) });
  }

  updateNote(noteId: string, note: any) {
    return this.http.put<any>(`/api/trading/notes/${noteId}`, note);
  }

  deleteNote(noteId: string) {
    return this.http.delete<any>(`/api/trading/notes/${noteId}`);
  }

  // --- Watchlists ---
  createWatchlist(data: any) {
    return this.http.post<any>('/api/watchlists', data);
  }

  getWatchlists() {
    return this.http.get<any>('/api/watchlists');
  }

  addWatchlistItem(watchlistId: string, data: any) {
    return this.http.post<any>(`/api/watchlists/${watchlistId}/items`, data);
  }

  removeWatchlistItem(watchlistId: string, symbol: string) {
    return this.http.delete<any>(`/api/watchlists/${watchlistId}/items/${symbol}`);
  }

  deleteWatchlist(watchlistId: string) {
    return this.http.delete<any>(`/api/watchlists/${watchlistId}`);
  }

  getWatchlistPrices(watchlistId: string) {
    return this.http.get<any>(`/api/watchlists/${watchlistId}/prices`);
  }

  // --- Strategies ---
  createStrategy(strategy: any) {
    return this.http.post<any>('/api/strategies', strategy);
  }

  createStrategyV2(strategy: any) {
    return this.http.post<any>('/api/strategies/v2', strategy);
  }

  getStrategies(params?: any) {
    return this.http.get<any>('/api/strategies', { params: this.toHttpParams(params) });
  }

  // --- Backtests ---
  runBacktest(backtest: any) {
    return this.http.post<any>('/api/backtests', backtest);
  }

  getBacktestResult(backtestRunId: string) {
    return this.http.get<any>(`/api/backtests/${backtestRunId}`);
  }

  getBacktests(params?: any) {
    return this.http.get<any>('/api/backtests', { params: this.toHttpParams(params) });
  }

  clearBacktestResults() {
    return this.http.delete<any>('/api/backtests');
  }

  getTradeChartData(backtestRunId: string, tradeIndex: number) {
    return this.http.get<any>(`/api/backtests/${backtestRunId}/trades/${tradeIndex}/chart`);
  }

  compareBacktests(ids: string[]) {
    return this.http.get<any>('/api/backtests/compare', {
      params: this.toHttpParams({ ids: ids.join(',') }),
    });
  }

  getBenchmarkData(startDate: string, endDate: string) {
    return this.http.get<any>('/api/universes/benchmark', {
      params: this.toHttpParams({ startDate, endDate }),
    });
  }

  // --- Stock Universes ---
  getUniverses() {
    return this.http.get<any>('/api/universes');
  }

  createUniverse(data: any) {
    return this.http.post<any>('/api/universes', data);
  }

  addUniverseSymbols(universeId: string, symbols: string[]) {
    return this.http.post<any>(`/api/universes/${universeId}/symbols`, { symbols });
  }

  removeUniverseSymbols(universeId: string, symbols: string[]) {
    return this.http.delete<any>(`/api/universes/${universeId}/symbols`, { body: { symbols } });
  }

  // --- Screener ---
  getScreenerResults(params?: any) {
    return this.http.get<any>('/api/screener/results', { params: this.toHttpParams(params) });
  }

  getScreenerSignal(symbol: string) {
    return this.http.get<any>(`/api/screener/results/${symbol}`);
  }

  getScreenerHistory(params?: any) {
    return this.http.get<any>('/api/screener/history', { params: this.toHttpParams(params) });
  }

  triggerScreenerScan(request?: any) {
    return this.http.post<any>('/api/screener/run', request ?? {});
  }

  runOptimization(params: any) {
    return this.http.post<any>('/api/backtests/optimize', params);
  }

  autoOptimize(backtestRunId: string) {
    return this.http.post<any>(`/api/backtests/${backtestRunId}/auto-optimize`, {});
  }

  getAutoOptimizeProgress(backtestRunId: string) {
    return this.http.get<any>(`/api/backtests/${backtestRunId}/auto-optimize/progress`);
  }

  getOptimizedParams(strategyId: string) {
    return this.http.get<any>(`/api/strategies/${strategyId}/optimized-params`);
  }

  // --- Intelligence ---
  detectStockRegime(symbol: string) {
    return this.http.get<any>(`/api/intelligence/detect-regime/${symbol}`);
  }

  // --- User Settings ---
  getUserSettings() {
    return this.http.get<any>('/api/user/settings');
  }

  updateUserSettings(settings: any) {
    return this.http.put<any>('/api/user/settings', settings);
  }

  // --- Audit ---
  getAuditLogs(params?: any) {
    return this.http.get<any>('/api/audit-logs', { params: this.toHttpParams(params) });
  }

  private toHttpParams(params?: Record<string, any>): HttpParams {
    let httpParams = new HttpParams();
    if (params) {
      for (const [key, value] of Object.entries(params)) {
        if (value !== undefined && value !== null) {
          httpParams = httpParams.set(key, String(value));
        }
      }
    }
    return httpParams;
  }
}
