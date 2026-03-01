import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    <div class="min-h-screen flex items-center justify-center bg-base-100">
      <div class="card w-96 bg-base-200 shadow-xl">
        <div class="card-body">
          <h2 class="card-title justify-center text-2xl mb-4">TradingAssistant</h2>
          <p class="text-center text-base-content/60 mb-4">Create a new account</p>

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

            <label class="floating-label">
              <span>Confirm Password</span>
              <input
                type="password"
                class="input input-bordered w-full"
                placeholder="Confirm Password"
                [(ngModel)]="confirmPassword"
                name="confirmPassword"
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
              Create Account
            </button>
          </form>

          <p class="text-center text-sm mt-4">
            Already have an account?
            <a routerLink="/login" class="link link-primary">Sign in</a>
          </p>
        </div>
      </div>
    </div>
  `,
})
export class RegisterComponent {
  private auth = inject(AuthService);
  private router = inject(Router);

  email = '';
  password = '';
  confirmPassword = '';
  loading = signal(false);
  error = signal('');

  onSubmit() {
    if (this.password !== this.confirmPassword) {
      this.error.set('Passwords do not match');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    this.auth.register({ email: this.email, password: this.password }).subscribe({
      next: () => {
        this.router.navigate(['/login']);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err.error?.detail ?? 'Registration failed');
      },
    });
  }
}
