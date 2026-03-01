import { Component, inject, signal, OnInit } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-backtest-list',
  standalone: true,
  imports: [RouterLink, DatePipe],
  template: `
    <div class="max-w-4xl">
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold">Backtest History</h2>
        <a routerLink="/backtests/new" class="btn btn-primary btn-sm">+ New Backtest</a>
      </div>

      @if (loading()) {
        <div class="flex items-center gap-3 p-8">
          <span class="loading loading-spinner loading-md"></span>
          <span class="text-sm">Loading backtests...</span>
        </div>
      } @else if (runs().length === 0) {
        <div class="text-center py-12 text-base-content/50">
          <div class="text-4xl mb-2">&#x1F9EA;</div>
          <p>No backtests yet. Run your first one!</p>
          <a routerLink="/backtests/new" class="btn btn-primary btn-sm mt-4">Create Backtest</a>
        </div>
      } @else {
        <div class="overflow-x-auto">
          <table class="table table-sm">
            <thead>
              <tr>
                <th>Strategy</th>
                <th>Symbol</th>
                <th>Period</th>
                <th>Status</th>
                <th class="text-right">Created</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              @for (run of runs(); track run.id) {
                <tr class="hover">
                  <td class="font-medium">{{ run.strategyName ?? '—' }}</td>
                  <td class="font-mono uppercase">{{ run.symbol }}</td>
                  <td class="text-xs font-mono">{{ run.startDate | date:'shortDate' }} – {{ run.endDate | date:'shortDate' }}</td>
                  <td>
                    <span class="badge badge-sm"
                          [class.badge-success]="run.status === 'Completed'"
                          [class.badge-warning]="run.status === 'Running' || run.status === 'Pending'"
                          [class.badge-error]="run.status === 'Failed'">
                      {{ run.status }}
                    </span>
                  </td>
                  <td class="text-right text-xs text-base-content/50">{{ run.createdAt | date:'short' }}</td>
                  <td>
                    <a [routerLink]="['/backtests', run.id]" class="btn btn-ghost btn-xs">View</a>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <!-- Pagination -->
        @if (totalPages() > 1) {
          <div class="flex justify-center gap-2 mt-4">
            <button class="btn btn-xs btn-ghost" [disabled]="page() <= 1" (click)="goPage(page() - 1)">&laquo;</button>
            <span class="text-xs self-center">Page {{ page() }} of {{ totalPages() }}</span>
            <button class="btn btn-xs btn-ghost" [disabled]="page() >= totalPages()" (click)="goPage(page() + 1)">&raquo;</button>
          </div>
        }
      }
    </div>
  `,
})
export class BacktestListComponent implements OnInit {
  private api = inject(ApiService);

  runs = signal<any[]>([]);
  loading = signal(true);
  page = signal(1);
  totalPages = signal(1);
  private pageSize = 20;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.getBacktests({ page: this.page(), pageSize: this.pageSize }).subscribe({
      next: (res) => {
        this.runs.set(res.items ?? []);
        this.totalPages.set(Math.ceil((res.totalCount ?? 0) / this.pageSize) || 1);
        this.loading.set(false);
      },
      error: () => {
        this.runs.set([]);
        this.loading.set(false);
      },
    });
  }

  goPage(p: number): void {
    this.page.set(p);
    this.load();
  }
}
