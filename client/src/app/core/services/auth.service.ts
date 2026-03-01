import { Injectable, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';

interface LoginResponse {
  token: string;
}

interface RegisterRequest {
  email: string;
  password: string;
}

interface LoginRequest {
  email: string;
  password: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly token = signal<string | null>(null);
  readonly isAuthenticated = computed(() => !!this.token());

  constructor(
    private http: HttpClient,
    private router: Router,
  ) {}

  getToken(): string | null {
    return this.token();
  }

  login(credentials: LoginRequest) {
    return this.http.post<LoginResponse>('/api/auth/login', credentials).pipe(
      tap((res) => this.token.set(res.token)),
    );
  }

  register(data: RegisterRequest) {
    return this.http.post('/api/auth/register', data);
  }

  logout() {
    this.token.set(null);
    this.router.navigate(['/login']);
  }
}
