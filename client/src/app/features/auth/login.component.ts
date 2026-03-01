import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-base-100">
      <div class="card w-96 bg-base-200 shadow-xl">
        <div class="card-body">
          <h2 class="card-title justify-center text-2xl mb-4">TradingAssistant</h2>
          <p class="text-center text-base-content/60 mb-4">Sign in to your account</p>

          @if (error()) {
            <div class="alert alert-error text-sm">{{ error() }}</div>
          }

          <form (ngSubmit)="onSubmit()" class="flex flex-col gap-3">
            <label class="floating-label">
              <span>Email</span>
              <input
                type="email"
                class="input input-bordered w-full"
                placeholder="Email"
                [(ngModel)]="email"
                name="email"
                required
              />
            </label>

            <label class="floating-label">
              <span>Password</span>
              <input
                type="password"
                class="input input-bordered w-full"
                placeholder="Password"
                [(ngModel)]="password"
                name="password"
                required
              />
            </label>

            <button
              type="submit"
              class="btn btn-primary w-full mt-2"
              [disabled]="loading()"
            >
              @if (loading()) {
                <span class="loading loading-spinner loading-sm"></span>
              }
              Sign In
            </button>
          </form>

          <p class="text-center text-sm mt-4">
            Don't have an account?
            <a routerLink="/register" class="link link-primary">Register</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class LoginComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  email = '';
  password = '';
  loading = signal(false);
  error = signal('');

  onSubmit() {
    this.loading.set(true);
    this.error.set('');

    this.auth.login({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.detail ?? 'Login failed');
      },
    });
  }
}
