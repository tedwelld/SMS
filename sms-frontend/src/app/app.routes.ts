import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'dashboard'
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard-page.component').then((m) => m.DashboardPageComponent)
  },
  {
    path: 'pos',
    loadComponent: () => import('./features/pos/pos-page.component').then((m) => m.PosPageComponent)
  },
  {
    path: 'inventory',
    loadComponent: () =>
      import('./features/inventory/inventory-page.component').then((m) => m.InventoryPageComponent)
  },
  {
    path: 'customers',
    loadComponent: () =>
      import('./features/customers/customers-page.component').then((m) => m.CustomersPageComponent)
  },
  {
    path: 'procurement',
    loadComponent: () =>
      import('./features/procurement/procurement-page.component').then((m) => m.ProcurementPageComponent)
  },
  {
    path: 'reports',
    loadComponent: () => import('./features/reports/reports-page.component').then((m) => m.ReportsPageComponent)
  },
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];
