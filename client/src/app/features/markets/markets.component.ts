import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';

interface StockUniverse {
  id: string;
  name: string;
  description: string;
  symbols: string[];
  isActive: boolean;
}

@Component({
  selector: 'app-markets',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="flex flex-col gap-4">
      <div class="flex items-center justify-between">
        <h2 class="text-2xl font-bold">Markets</h2>
        <span class="text-sm text-base-content/50">Click any symbol to view its chart</span>
      </div>

      @if (loading()) {
        <div class="flex justify-center p-12">
          <span class="loading loading-spinner loading-lg"></span>
        </div>
      } @else if (error()) {
        <div class="alert alert-error text-sm">{{ error() }}</div>
      } @else {

        <!-- Search -->
        <div class="form-control">
          <input type="text" class="input input-bordered input-sm w-64 font-mono uppercase"
                 placeholder="Search symbol..."
                 [value]="search()"
                 (input)="onSearch($event)" />
        </div>

        @for (universe of filteredUniverses(); track universe.id) {
          <div class="card bg-base-200">
            <div class="card-body p-4">
              <!-- Index header -->
              <div class="flex items-center gap-3 cursor-pointer select-none"
                   (click)="toggleExpanded(universe.id)">
                <span class="text-lg transition-transform"
                      [class.rotate-90]="expanded().has(universe.id)">&#9654;</span>
                <div>
                  <h3 class="font-bold text-lg">{{ universe.name }}</h3>
                  <p class="text-xs text-base-content/50">{{ universe.description }}</p>
                </div>
                <span class="badge badge-outline ml-auto">{{ universe.symbols.length }} stocks</span>
              </div>

              <!-- Stock grid (collapsible) -->
              @if (expanded().has(universe.id)) {
                <div class="grid grid-cols-5 sm:grid-cols-6 md:grid-cols-8 lg:grid-cols-10 gap-2 mt-3">
                  @for (sym of getFilteredSymbols(universe); track sym) {
                    <a [routerLink]="'/charts'"
                       [queryParams]="{ symbol: sym }"
                       class="btn btn-ghost btn-sm font-mono text-xs h-auto py-2 px-1 hover:btn-primary">
                      {{ sym }}
                    </a>
                  }
                </div>

                <!-- Quick actions -->
                <div class="flex gap-2 mt-3 pt-3 border-t border-base-300">
                  <button class="btn btn-outline btn-xs"
                          (click)="ingestAll(universe); $event.stopPropagation()"
                          [disabled]="ingesting().has(universe.id)">
                    @if (ingesting().has(universe.id)) {
                      <span class="loading loading-spinner loading-xs"></span>
                      Fetching data...
                    } @else {
                      Fetch All Data (Yahoo Finance)
                    }
                  </button>
                  <span class="text-xs text-base-content/40 self-center">
                    Downloads 5 years of price history for all stocks in this index
                  </span>
                </div>
              }
            </div>
          </div>
        }

        @if (filteredUniverses().length === 0 && search()) {
          <div class="text-center text-base-content/50 p-8">
            No stocks match "{{ search() }}"
          </div>
        }
      }
    </div>
  `,
})
export class MarketsComponent implements OnInit {
  private api = inject(ApiService);

  loading = signal(true);
  error = signal('');
  universes = signal<StockUniverse[]>([]);
  expanded = signal(new Set<string>());
  search = signal('');
  ingesting = signal(new Set<string>());

  ngOnInit(): void {
    this.api.getUniverses().subscribe({
      next: (res: any) => {
        const list: StockUniverse[] = Array.isArray(res) ? res : res.items ?? res ?? [];
        this.universes.set(list);
        // Auto-expand the first universe
        if (list.length > 0) {
          const s = new Set<string>();
          s.add(list[0].id);
          this.expanded.set(s);
        }
        this.loading.set(false);
      },
      error: (err: any) => {
        this.error.set(err.error?.detail ?? 'Failed to load market indices');
        this.loading.set(false);
      },
    });
  }

  filteredUniverses(): StockUniverse[] {
    const q = this.search().toUpperCase();
    if (!q) return this.universes();
    return this.universes()
      .map(u => ({
        ...u,
        symbols: u.symbols.filter(s => s.includes(q)),
      }))
      .filter(u => u.symbols.length > 0 || u.name.toUpperCase().includes(q));
  }

  getFilteredSymbols(universe: StockUniverse): string[] {
    const q = this.search().toUpperCase();
    if (!q) return universe.symbols;
    return universe.symbols.filter(s => s.includes(q));
  }

  toggleExpanded(id: string): void {
    const s = new Set(this.expanded());
    if (s.has(id)) s.delete(id);
    else s.add(id);
    this.expanded.set(s);
  }

  onSearch(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.search.set(val);
    // Auto-expand all universes when searching
    if (val) {
      this.expanded.set(new Set(this.universes().map(u => u.id)));
    }
  }

  ingestAll(universe: StockUniverse): void {
    const s = new Set(this.ingesting());
    s.add(universe.id);
    this.ingesting.set(s);

    let completed = 0;
    const total = universe.symbols.length;

    for (const sym of universe.symbols) {
      this.api.ingestMarketData(sym, 5).subscribe({
        next: () => {
          completed++;
          if (completed >= total) this.finishIngesting(universe.id);
        },
        error: () => {
          completed++;
          if (completed >= total) this.finishIngesting(universe.id);
        },
      });
    }
  }

  private finishIngesting(id: string): void {
    const s = new Set(this.ingesting());
    s.delete(id);
    this.ingesting.set(s);
  }
}
