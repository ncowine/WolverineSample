import { Component, inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-header',
  standalone: true,
  template: `
    <header class="h-14 bg-base-200 border-b border-base-300 flex items-center justify-between px-4">
      <h1 class="text-lg font-semibold">TradingAssistant</h1>
      <div class="flex items-center gap-3">
        <span class="badge badge-success badge-sm gap-1">
          <span class="w-2 h-2 rounded-full bg-success"></span>
          Connected
        </span>
        <button class="btn btn-ghost btn-sm" (click)="auth.logout()">Logout</button>
      </div>
    </header>
  `,
})
export class HeaderComponent {
  auth = inject(AuthService);
}
