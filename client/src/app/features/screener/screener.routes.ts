import { Routes } from '@angular/router';
import { ScreenerComponent } from './screener.component';
import { ScreenerDetailComponent } from './screener-detail.component';

export const screenerRoutes: Routes = [
  { path: '', component: ScreenerComponent },
  { path: ':symbol', component: ScreenerDetailComponent },
];
