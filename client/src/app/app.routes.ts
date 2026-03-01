import { Routes } from '@angular/router';
import { ShellComponent } from './core/layout/shell.component';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // Public auth routes
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register.component').then((m) => m.RegisterComponent),
  },

  // Guarded routes inside shell layout
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.dashboardRoutes),
      },
      {
        path: 'portfolio',
        loadChildren: () =>
          import('./features/portfolio/portfolio.routes').then((m) => m.portfolioRoutes),
      },
      {
        path: 'orders',
        loadChildren: () =>
          import('./features/orders/orders.routes').then((m) => m.ordersRoutes),
      },
      {
        path: 'dca',
        loadChildren: () =>
          import('./features/dca/dca.routes').then((m) => m.dcaRoutes),
      },
      {
        path: 'watchlists',
        loadChildren: () =>
          import('./features/watchlists/watchlists.routes').then((m) => m.watchlistsRoutes),
      },
      {
        path: 'journal',
        loadChildren: () =>
          import('./features/journal/journal.routes').then((m) => m.journalRoutes),
      },
      {
        path: 'charts',
        loadChildren: () =>
          import('./features/charts/charts.routes').then((m) => m.chartsRoutes),
      },
      {
        path: 'backtests',
        loadChildren: () =>
          import('./features/backtests/backtests.routes').then((m) => m.backtestsRoutes),
      },
      {
        path: 'screener',
        loadChildren: () =>
          import('./features/screener/screener.routes').then((m) => m.screenerRoutes),
      },
    ],
  },

  // Catch-all redirect
  { path: '**', redirectTo: '' },
];
