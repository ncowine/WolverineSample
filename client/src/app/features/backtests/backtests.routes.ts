import { Routes } from '@angular/router';
import { BacktestListComponent } from './backtest-list.component';
import { BacktestConfigComponent } from './backtest-config.component';
import { BacktestResultComponent } from './backtest-result.component';

export const backtestsRoutes: Routes = [
  { path: '', component: BacktestListComponent },
  { path: 'new', component: BacktestConfigComponent },
  { path: ':id', component: BacktestResultComponent },
];
