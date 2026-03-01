import { Component, inject, signal } from '@angular/core';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <div class="max-w-4xl">
      <h2 class="text-2xl font-bold mb-6">Dashboard</h2>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 mb-8">
        <div class="card bg-base-200">
          <div class="card-body">
            <h3 class="card-title text-sm text-base-content/60">Portfolio</h3>
            <p class="text-2xl font-bold">--</p>
          </div>
        </div>
        <div class="card bg-base-200">
          <div class="card-body">
            <h3 class="card-title text-sm text-base-content/60">Open Orders</h3>
            <p class="text-2xl font-bold">--</p>
          </div>
        </div>
        <div class="card bg-base-200">
          <div class="card-body">
            <h3 class="card-title text-sm text-base-content/60">DCA Plans</h3>
            <p class="text-2xl font-bold">--</p>
          </div>
        </div>
      </div>

      <div class="card bg-base-200">
        <div class="card-body">
          <h3 class="card-title">Getting Started</h3>
          <p class="text-base-content/60 mb-4">
            Seed market data to get started with paper trading.
          </p>
          <button
            class="btn btn-primary btn-sm w-fit"
            (click)="seedData()"
            [disabled]="seeding()"
          >
            @if (seeding()) {
              <span class="loading loading-spinner loading-sm"></span>
            }
            Seed Market Data
          </button>
          @if (seedResult()) {
            <div class="alert alert-success text-sm mt-2">{{ seedResult() }}</div>
          }
        </div>
      </div>
    </div>
  `,
})
export class DashboardComponent {
  private api = inject(ApiService);
  seeding = signal(false);
  seedResult = signal('');

  seedData() {
    this.seeding.set(true);
    this.seedResult.set('');
    this.api.seedMarketData().subscribe({
      next: () => {
        this.seeding.set(false);
        this.seedResult.set('Market data seeded successfully!');
      },
      error: () => {
        this.seeding.set(false);
        this.seedResult.set('Seeding failed. Is the backend running?');
      },
    });
  }
}
