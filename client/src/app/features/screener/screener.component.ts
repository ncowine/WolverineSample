import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-screener',
  standalone: true,
  imports: [DecimalPipe, DatePipe, FormsModule],
  template: `
    <div class="max-w-6xl">
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold">Stock Screener</h2>
        <button class="btn btn-primary btn-sm" (click)="triggerScan()" [disabled]="scanning()">
          @if (scanning()) {
            <span class="loading loading-spinner loading-xs"></span>
          }
          Run Scan
        </button>
      </div>

      <!-- Filters -->
      <div class="flex flex-wrap gap-3 mb-4 items-end">
        <div class="form-control">
          <label class="label"><span class="label-text text-xs">Min Grade</span></label>
          <select class="select select-bordered select-sm w-32" [(ngModel)]="gradeFilter" (ngModelChange)="loadResults()">
            <option value="">All</option>
            <option value="A">A only</option>
            <option value="B">A + B</option>
            <option value="C">A + B + C</option>
          </select>
        </div>
        <div class="form-control">
          <label class="label"><span class="label-text text-xs">Sort By</span></label>
          <select class="select select-bordered select-sm w-36" [(ngModel)]="sortKey" (ngModelChange)="sortChanged()">
            <option value="score">Score</option>
            <option value="grade">Grade</option>
            <option value="riskRewardRatio">R:R Ratio</option>
            <option value="symbol">Symbol</option>
          </select>
        </div>
        @if (scanInfo()) {
          <div class="text-xs text-base-content/50 ml-auto self-center">
            {{ scanInfo()!.symbolsScanned }} symbols scanned &middot;
            {{ scanInfo()!.signalsFound }} signals found &middot;
            {{ scanInfo()!.signalsPassingFilter }} passing filter
          </div>
        }
      </div>

      <!-- Warnings -->
      @if (warnings().length > 0) {
        <div class="alert alert-warning text-xs mb-4">
          @for (w of warnings(); track $index) {
            <div>{{ w }}</div>
          }
        </div>
      }

      @if (loading()) {
        <div class="flex items-center gap-3 p-8">
          <span class="loading loading-spinner loading-md"></span>
          <span class="text-sm">Loading screener results...</span>
        </div>
      } @else if (sortedSignals().length === 0) {
        <div class="text-center py-12 text-base-content/50">
          <div class="text-4xl mb-2">&#x1F50D;</div>
          <p>No signals found. Run a scan to get started.</p>
        </div>
      } @else {
        <!-- Results Table -->
        <div class="overflow-x-auto">
          <table class="table table-sm">
            <thead>
              <tr>
                <th class="cursor-pointer" (click)="toggleSort('symbol')">Symbol</th>
                <th class="cursor-pointer" (click)="toggleSort('grade')">Grade</th>
                <th class="cursor-pointer text-right" (click)="toggleSort('score')">Score</th>
                <th>Dir</th>
                <th class="text-right">Entry</th>
                <th class="text-right">Stop</th>
                <th class="text-right">Target</th>
                <th class="cursor-pointer text-right" (click)="toggleSort('riskRewardRatio')">R:R</th>
                <th class="text-right">Win Rate</th>
                <th>Date</th>
              </tr>
            </thead>
            <tbody>
              @for (s of sortedSignals(); track s.symbol) {
                <tr class="hover cursor-pointer" (click)="viewDetail(s.symbol)">
                  <td class="font-mono font-bold">{{ s.symbol }}</td>
                  <td>
                    <span class="badge badge-sm font-bold"
                          [class.badge-success]="s.grade === 'A'"
                          [class.badge-info]="s.grade === 'B'"
                          [class.badge-warning]="s.grade === 'C'"
                          [class.badge-neutral]="s.grade === 'D'"
                          [class.badge-error]="s.grade === 'F'">
                      {{ s.grade }}
                    </span>
                  </td>
                  <td class="text-right font-mono">{{ s.score | number:'1.0-0' }}</td>
                  <td>
                    <span class="text-xs" [class.text-success]="s.direction === 'Long'" [class.text-error]="s.direction === 'Short'">
                      {{ s.direction }}
                    </span>
                  </td>
                  <td class="text-right font-mono">{{ s.entryPrice | number:'1.2-2' }}</td>
                  <td class="text-right font-mono text-error">{{ s.stopPrice | number:'1.2-2' }}</td>
                  <td class="text-right font-mono text-success">{{ s.targetPrice | number:'1.2-2' }}</td>
                  <td class="text-right font-mono font-bold">{{ s.riskRewardRatio | number:'1.1-1' }}</td>
                  <td class="text-right font-mono">
                    @if (s.historicalWinRate !== null && s.historicalWinRate !== undefined) {
                      {{ s.historicalWinRate | number:'1.0-0' }}%
                    } @else {
                      <span class="text-base-content/30">—</span>
                    }
                  </td>
                  <td class="text-xs text-base-content/50">{{ s.signalDate | date:'shortDate' }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }

      <!-- Scan History -->
      <div class="collapse collapse-arrow bg-base-200 mt-6">
        <input type="checkbox" />
        <div class="collapse-title text-sm font-medium">Scan History</div>
        <div class="collapse-content">
          @if (history().length === 0) {
            <p class="text-sm text-base-content/50">No scan history yet.</p>
          } @else {
            <table class="table table-xs">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Strategy</th>
                  <th class="text-right">Symbols</th>
                  <th class="text-right">Signals</th>
                </tr>
              </thead>
              <tbody>
                @for (h of history(); track h.id) {
                  <tr>
                    <td class="font-mono text-xs">{{ h.scanDate | date:'short' }}</td>
                    <td>{{ h.strategyName ?? '—' }}</td>
                    <td class="text-right font-mono">{{ h.symbolsScanned }}</td>
                    <td class="text-right font-mono">{{ h.signalsFound }}</td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </div>
      </div>
    </div>
  `,
})
export class ScreenerComponent implements OnInit {
  private api = inject(ApiService);
  private router = inject(Router);

  loading = signal(true);
  scanning = signal(false);
  signals = signal<any[]>([]);
  scanInfo = signal<any>(null);
  warnings = signal<string[]>([]);
  history = signal<any[]>([]);

  gradeFilter = '';
  sortKey = 'score';
  sortAsc = signal(false);

  private gradeOrder: Record<string, number> = { A: 0, B: 1, C: 2, D: 3, F: 4 };

  sortedSignals = computed(() => {
    const list = [...this.signals()];
    const key = this.sortKey;
    const asc = this.sortAsc();
    list.sort((a, b) => {
      let va = a[key]; let vb = b[key];
      if (key === 'grade') { va = this.gradeOrder[va] ?? 5; vb = this.gradeOrder[vb] ?? 5; }
      if (typeof va === 'number' && typeof vb === 'number') return asc ? va - vb : vb - va;
      return asc ? String(va).localeCompare(String(vb)) : String(vb).localeCompare(String(va));
    });
    return list;
  });

  ngOnInit(): void {
    this.loadResults();
    this.loadHistory();
  }

  loadResults(): void {
    this.loading.set(true);
    const params: any = {};
    if (this.gradeFilter) params.minGrade = this.gradeFilter;

    this.api.getScreenerResults(params).subscribe({
      next: (res) => {
        this.signals.set(res.signals ?? []);
        this.scanInfo.set({ symbolsScanned: res.symbolsScanned, signalsFound: res.signalsFound, signalsPassingFilter: res.signalsPassingFilter });
        this.warnings.set(res.warnings ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.signals.set([]);
        this.loading.set(false);
      },
    });
  }

  loadHistory(): void {
    this.api.getScreenerHistory({ page: 1, pageSize: 10 }).subscribe({
      next: (res) => this.history.set(res.items ?? []),
      error: () => this.history.set([]),
    });
  }

  triggerScan(): void {
    this.scanning.set(true);
    this.api.triggerScreenerScan({}).subscribe({
      next: (res) => {
        this.signals.set(res.signals ?? []);
        this.scanInfo.set({ symbolsScanned: res.symbolsScanned, signalsFound: res.signalsFound, signalsPassingFilter: res.signalsPassingFilter });
        this.warnings.set(res.warnings ?? []);
        this.scanning.set(false);
        this.loadHistory();
      },
      error: () => this.scanning.set(false),
    });
  }

  viewDetail(symbol: string): void {
    this.router.navigate(['/screener', symbol]);
  }

  toggleSort(key: string): void {
    if (this.sortKey === key) {
      this.sortAsc.set(!this.sortAsc());
    } else {
      this.sortKey = key;
      this.sortAsc.set(key === 'symbol');
    }
  }

  sortChanged(): void {
    this.sortAsc.set(this.sortKey === 'symbol');
  }
}
