import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div class="max-w-xl">
      <h2 class="text-2xl font-bold mb-6">Settings</h2>

      @if (loading()) {
        <div class="flex items-center gap-3">
          <span class="loading loading-spinner loading-md"></span>
          <span class="text-sm">Loading settings...</span>
        </div>
      } @else {
        <div class="card bg-base-200">
          <div class="card-body gap-4">
            <h3 class="card-title text-lg">Preferences</h3>

            <div class="form-control">
              <label class="label"><span class="label-text text-xs">Default Currency</span></label>
              <select class="select select-bordered select-sm" [(ngModel)]="defaultCurrency">
                @for (c of currencies; track c.code) {
                  <option [value]="c.code">{{ c.code }} — {{ c.symbol }} {{ c.name }}</option>
                }
              </select>
            </div>

            <div class="form-control">
              <label class="label"><span class="label-text text-xs">Default Initial Capital ({{ currencySymbol() }})</span></label>
              <input type="number" class="input input-bordered input-sm" [(ngModel)]="defaultInitialCapital" step="100" min="0" />
            </div>

            <div class="form-control">
              <label class="label"><span class="label-text text-xs">Cost Profile Market</span></label>
              <select class="select select-bordered select-sm" [(ngModel)]="costProfileMarket">
                <option value="UK">UK (LSE — stamp duty, FX fees)</option>
                <option value="US">US (NYSE/NASDAQ — per-share commission)</option>
                <option value="IN">India (NSE/BSE — STT, brokerage)</option>
              </select>
            </div>

            @if (error()) {
              <div class="alert alert-error text-sm">{{ error() }}</div>
            }
            @if (saved()) {
              <div class="alert alert-success text-sm">Settings saved.</div>
            }

            <div class="card-actions justify-end mt-2">
              <button class="btn btn-primary btn-sm" (click)="save()" [disabled]="saving()">
                @if (saving()) {
                  <span class="loading loading-spinner loading-xs"></span>
                }
                Save Settings
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class SettingsComponent {
  private api = inject(ApiService);

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  saved = signal(false);

  defaultCurrency = 'GBP';
  defaultInitialCapital = 1000;
  costProfileMarket = 'UK';

  currencies = [
    { code: 'GBP', symbol: '\u00A3', name: 'British Pound' },
    { code: 'USD', symbol: '$', name: 'US Dollar' },
    { code: 'EUR', symbol: '\u20AC', name: 'Euro' },
    { code: 'INR', symbol: '\u20B9', name: 'Indian Rupee' },
  ];

  currencySymbol = () => this.currencies.find(c => c.code === this.defaultCurrency)?.symbol ?? this.defaultCurrency;

  constructor() {
    this.api.getUserSettings().subscribe({
      next: (s: any) => {
        this.defaultCurrency = s.defaultCurrency ?? 'GBP';
        this.defaultInitialCapital = s.defaultInitialCapital ?? 1000;
        this.costProfileMarket = s.costProfileMarket ?? 'UK';
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(): void {
    this.saving.set(true);
    this.error.set('');
    this.saved.set(false);
    this.api.updateUserSettings({
      defaultCurrency: this.defaultCurrency,
      defaultInitialCapital: this.defaultInitialCapital,
      costProfileMarket: this.costProfileMarket,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 3000);
      },
      error: (err: any) => {
        this.saving.set(false);
        this.error.set(err.error?.detail ?? err.message ?? 'Failed to save');
      },
    });
  }
}
