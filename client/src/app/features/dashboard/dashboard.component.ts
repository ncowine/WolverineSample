import { Component, inject, signal, OnInit } from '@angular/core';
import { DecimalPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { forkJoin } from 'rxjs';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DecimalPipe, DatePipe, RouterLink],
  template: `
    <div class="max-w-6xl">
      <div class="flex items-center justify-between mb-6">
        <h2 class="text-2xl font-bold">Dashboard</h2>
        <div class="text-xs text-base-content/40">{{ today | date:'fullDate' }}</div>
      </div>

      <!-- Market Overview -->
      <div class="grid grid-cols-2 md:grid-cols-4 gap-3 mb-6">
        <div class="bg-base-200 rounded-lg p-4">
          <div class="text-[10px] text-base-content/50 uppercase tracking-wider">SPY</div>
          @if (spyLoading()) {
            <div class="skeleton h-7 w-20 mt-1"></div>
          } @else if (spy()) {
            <div class="text-xl font-bold font-mono">{{ spy()!.price | number:'1.2-2' }}</div>
            <div class="text-xs font-mono" [class.text-success]="spy()!.change >= 0" [class.text-error]="spy()!.change < 0">
              {{ spy()!.change >= 0 ? '+' : '' }}{{ spy()!.changePercent | number:'1.2-2' }}%
            </div>
          } @else {
            <div class="text-lg text-base-content/30">—</div>
          }
        </div>

        <div class="bg-base-200 rounded-lg p-4">
          <div class="text-[10px] text-base-content/50 uppercase tracking-wider">Portfolio Value</div>
          @if (portfolioLoading()) {
            <div class="skeleton h-7 w-24 mt-1"></div>
          } @else if (portfolio()) {
            <div class="text-xl font-bold font-mono">\${{ portfolio()!.totalValue | number:'1.0-0' }}</div>
            <div class="text-xs font-mono" [class.text-success]="portfolio()!.dailyPnl >= 0" [class.text-error]="portfolio()!.dailyPnl < 0">
              {{ portfolio()!.dailyPnl >= 0 ? '+' : '' }}\${{ portfolio()!.dailyPnl | number:'1.2-2' }}
            </div>
          } @else {
            <div class="text-lg text-base-content/30">—</div>
          }
        </div>

        <div class="bg-base-200 rounded-lg p-4">
          <div class="text-[10px] text-base-content/50 uppercase tracking-wider">Open Positions</div>
          @if (portfolioLoading()) {
            <div class="skeleton h-7 w-12 mt-1"></div>
          } @else {
            <div class="text-xl font-bold font-mono">{{ positionCount() }}</div>
          }
        </div>

        <div class="bg-base-200 rounded-lg p-4">
          <div class="text-[10px] text-base-content/50 uppercase tracking-wider">Active DCA Plans</div>
          @if (dcaLoading()) {
            <div class="skeleton h-7 w-12 mt-1"></div>
          } @else {
            <div class="text-xl font-bold font-mono">{{ dcaCount() }}</div>
          }
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <!-- Top Signals Widget -->
        <div class="card bg-base-200">
          <div class="card-body p-4 gap-3">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold">Top Signals</h3>
              <a routerLink="/screener" class="btn btn-ghost btn-xs">View All</a>
            </div>

            @if (signalsLoading()) {
              <div class="space-y-2">
                @for (_ of [1,2,3]; track _) {
                  <div class="skeleton h-8 w-full"></div>
                }
              </div>
            } @else if (topSignals().length === 0) {
              <div class="text-sm text-base-content/50 py-4 text-center">
                No signals yet.
                <button class="link link-primary" (click)="runScan()">Run a scan</button>
              </div>
            } @else {
              <table class="table table-xs">
                <tbody>
                  @for (s of topSignals(); track s.symbol) {
                    <tr class="hover cursor-pointer" [routerLink]="['/screener', s.symbol]">
                      <td class="font-mono font-bold">{{ s.symbol }}</td>
                      <td>
                        <span class="badge badge-xs font-bold"
                              [class.badge-success]="s.grade === 'A'"
                              [class.badge-info]="s.grade === 'B'">
                          {{ s.grade }}
                        </span>
                      </td>
                      <td class="font-mono text-right">{{ s.score | number:'1.0-0' }}</td>
                      <td class="text-xs" [class.text-success]="s.direction === 'Long'" [class.text-error]="s.direction === 'Short'">
                        {{ s.direction }}
                      </td>
                      <td class="font-mono text-right text-xs">R:R {{ s.riskRewardRatio | number:'1.1-1' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        </div>

        <!-- Recent Backtests -->
        <div class="card bg-base-200">
          <div class="card-body p-4 gap-3">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold">Recent Backtests</h3>
              <a routerLink="/backtests" class="btn btn-ghost btn-xs">View All</a>
            </div>

            @if (backtestsLoading()) {
              <div class="space-y-2">
                @for (_ of [1,2,3]; track _) {
                  <div class="skeleton h-8 w-full"></div>
                }
              </div>
            } @else if (recentBacktests().length === 0) {
              <div class="text-sm text-base-content/50 py-4 text-center">
                No backtests yet.
                <a routerLink="/backtests/new" class="link link-primary">Run one</a>
              </div>
            } @else {
              <table class="table table-xs">
                <tbody>
                  @for (b of recentBacktests(); track b.id) {
                    <tr class="hover cursor-pointer" [routerLink]="['/backtests', b.id]">
                      <td class="font-mono font-bold">{{ b.symbol }}</td>
                      <td class="text-xs">{{ b.strategyName ?? '—' }}</td>
                      <td>
                        <span class="badge badge-xs"
                              [class.badge-success]="b.status === 'Completed'"
                              [class.badge-warning]="b.status === 'Running'"
                              [class.badge-error]="b.status === 'Failed'">
                          {{ b.status }}
                        </span>
                      </td>
                      <td class="text-xs text-base-content/50 text-right">{{ b.createdAt | date:'shortDate' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        </div>

        <!-- Active DCA Plans -->
        <div class="card bg-base-200">
          <div class="card-body p-4 gap-3">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold">Active DCA Plans</h3>
              <a routerLink="/dca" class="btn btn-ghost btn-xs">Manage</a>
            </div>

            @if (dcaLoading()) {
              <div class="space-y-2">
                @for (_ of [1,2]; track _) {
                  <div class="skeleton h-8 w-full"></div>
                }
              </div>
            } @else if (dcaPlans().length === 0) {
              <div class="text-sm text-base-content/50 py-4 text-center">
                No active DCA plans.
                <a routerLink="/dca" class="link link-primary">Create one</a>
              </div>
            } @else {
              <table class="table table-xs">
                <tbody>
                  @for (d of dcaPlans(); track d.id) {
                    <tr>
                      <td class="font-mono font-bold">{{ d.symbol }}</td>
                      <td class="text-xs">{{ d.frequency }}</td>
                      <td class="font-mono text-right text-xs">\${{ d.amount | number:'1.2-2' }}</td>
                      <td>
                        <span class="badge badge-xs"
                              [class.badge-success]="d.status === 'Active'"
                              [class.badge-warning]="d.status === 'Paused'">
                          {{ d.status }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            }
          </div>
        </div>

        <!-- Quick Actions -->
        <div class="card bg-base-200">
          <div class="card-body p-4 gap-3">
            <h3 class="text-sm font-semibold">Quick Actions</h3>
            <div class="grid grid-cols-2 gap-2">
              <button class="btn btn-sm btn-outline" (click)="runScan()" [disabled]="scanning()">
                @if (scanning()) {
                  <span class="loading loading-spinner loading-xs"></span>
                }
                Run Screener
              </button>
              <a routerLink="/backtests/new" class="btn btn-sm btn-outline">New Backtest</a>
              <a routerLink="/orders" class="btn btn-sm btn-outline">Place Order</a>
              <a routerLink="/charts" class="btn btn-sm btn-outline">View Charts</a>
            </div>

            <!-- Seed data (first-time setup) -->
            @if (!portfolio() && !portfolioLoading()) {
              <div class="divider text-xs my-1">Setup</div>
              <button class="btn btn-primary btn-sm" (click)="seedData()" [disabled]="seeding()">
                @if (seeding()) {
                  <span class="loading loading-spinner loading-xs"></span>
                }
                Seed Market Data
              </button>
              @if (seedResult()) {
                <div class="text-xs" [class.text-success]="seedResult() === 'done'" [class.text-error]="seedResult() !== 'done'">
                  {{ seedResult() === 'done' ? 'Market data seeded!' : seedResult() }}
                </div>
              }
            }
          </div>
        </div>
      </div>
    </div>
  `,
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiService);

  today = new Date();

  // Market overview
  spyLoading = signal(true);
  spy = signal<{ price: number; change: number; changePercent: number } | null>(null);

  // Portfolio
  portfolioLoading = signal(true);
  portfolio = signal<{ totalValue: number; dailyPnl: number } | null>(null);
  positionCount = signal(0);

  // DCA
  dcaLoading = signal(true);
  dcaPlans = signal<any[]>([]);
  dcaCount = signal(0);

  // Signals
  signalsLoading = signal(true);
  topSignals = signal<any[]>([]);

  // Backtests
  backtestsLoading = signal(true);
  recentBacktests = signal<any[]>([]);

  // Actions
  scanning = signal(false);
  seeding = signal(false);
  seedResult = signal('');

  // Account ID — will be discovered from paper account creation
  private accountId: string | null = null;

  ngOnInit(): void {
    this.loadSpy();
    this.loadSignals();
    this.loadBacktests();
    this.loadAccountData();
  }

  private loadSpy(): void {
    this.api.getStockPrice('SPY').subscribe({
      next: (res) => {
        this.spy.set({
          price: res.price ?? res.close ?? 0,
          change: res.change ?? 0,
          changePercent: res.changePercent ?? 0,
        });
        this.spyLoading.set(false);
      },
      error: () => this.spyLoading.set(false),
    });
  }

  private loadSignals(): void {
    this.api.getScreenerResults({ minGrade: 'B' }).subscribe({
      next: (res) => {
        const signals = (res.signals ?? []).slice(0, 5);
        this.topSignals.set(signals);
        this.signalsLoading.set(false);
      },
      error: () => this.signalsLoading.set(false),
    });
  }

  private loadBacktests(): void {
    this.api.getBacktests({ page: 1, pageSize: 3 }).subscribe({
      next: (res) => {
        this.recentBacktests.set(res.items ?? []);
        this.backtestsLoading.set(false);
      },
      error: () => this.backtestsLoading.set(false),
    });
  }

  private loadAccountData(): void {
    // Try to create/get paper account, then load portfolio + DCA
    this.api.createPaperAccount().subscribe({
      next: (account) => {
        this.accountId = account.id ?? account.accountId;
        if (this.accountId) {
          this.loadPortfolio(this.accountId);
          this.loadDca(this.accountId);
        } else {
          this.portfolioLoading.set(false);
          this.dcaLoading.set(false);
        }
      },
      error: () => {
        this.portfolioLoading.set(false);
        this.dcaLoading.set(false);
      },
    });
  }

  private loadPortfolio(accountId: string): void {
    forkJoin({
      portfolio: this.api.getPortfolio(accountId),
      positions: this.api.getPositions(accountId),
    }).subscribe({
      next: ({ portfolio, positions }) => {
        this.portfolio.set({
          totalValue: portfolio.totalValue ?? portfolio.cashBalance ?? 0,
          dailyPnl: portfolio.dailyPnl ?? portfolio.unrealizedPnl ?? 0,
        });
        const posList = Array.isArray(positions) ? positions : positions.items ?? [];
        this.positionCount.set(posList.length);
        this.portfolioLoading.set(false);
      },
      error: () => this.portfolioLoading.set(false),
    });
  }

  private loadDca(accountId: string): void {
    this.api.getDcaPlans(accountId).subscribe({
      next: (plans) => {
        const list = Array.isArray(plans) ? plans : plans.items ?? [];
        const active = list.filter((p: any) => p.status === 'Active' || p.status === 'Paused');
        this.dcaPlans.set(active.slice(0, 5));
        this.dcaCount.set(active.filter((p: any) => p.status === 'Active').length);
        this.dcaLoading.set(false);
      },
      error: () => this.dcaLoading.set(false),
    });
  }

  runScan(): void {
    this.scanning.set(true);
    this.api.triggerScreenerScan({}).subscribe({
      next: (res) => {
        const signals = (res.signals ?? []).slice(0, 5);
        this.topSignals.set(signals);
        this.scanning.set(false);
      },
      error: () => this.scanning.set(false),
    });
  }

  seedData(): void {
    this.seeding.set(true);
    this.seedResult.set('');
    this.api.seedMarketData().subscribe({
      next: () => {
        this.seeding.set(false);
        this.seedResult.set('done');
      },
      error: () => {
        this.seeding.set(false);
        this.seedResult.set('Seeding failed. Is the backend running?');
      },
    });
  }
}
