import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="w-16 bg-base-200 flex flex-col items-center py-4 gap-2 border-r border-base-300 h-full">
      <div class="text-2xl font-bold mb-4">T</div>

      @for (item of navItems; track item.route) {
        <a
          [routerLink]="item.route"
          routerLinkActive="bg-primary text-primary-content"
          class="btn btn-ghost btn-square btn-sm tooltip tooltip-right"
          [attr.data-tip]="item.label"
        >
          <span class="text-lg">{{ item.icon }}</span>
        </a>
      }
    </aside>
  `,
})
export class SidebarComponent {
  navItems = [
    { route: '/', icon: '\u{1F4CA}', label: 'Dashboard' },
    { route: '/charts', icon: '\u{1F4C8}', label: 'Charts' },
    { route: '/portfolio', icon: '\u{1F4BC}', label: 'Portfolio' },
    { route: '/orders', icon: '\u{1F4DD}', label: 'Orders' },
    { route: '/dca', icon: '\u{1F504}', label: 'DCA Plans' },
    { route: '/watchlists', icon: '\u{1F440}', label: 'Watchlists' },
    { route: '/journal', icon: '\u{1F4D3}', label: 'Journal' },
  ];
}
