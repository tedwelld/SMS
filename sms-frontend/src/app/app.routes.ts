import { Routes } from '@angular/router';

import { authGuard } from './core/auth.guard';
import { AppLayout } from './layout/component/app.layout';

export const routes: Routes = [
  {
    path: 'auth/login',
    loadComponent: () =>
      import('./features/auth/login/login-page.component').then((m) => m.LoginPageComponent)
  },
  {
    path: 'auth/create-account',
    loadComponent: () =>
      import('./features/auth/create-account/create-account-page.component').then(
        (m) => m.CreateAccountPageComponent
      )
  },
  {
    path: 'auth/forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password-page.component').then(
        (m) => m.ForgotPasswordPageComponent
      )
  },
  {
    path: '',
    canActivate: [authGuard],
    component: AppLayout,
    children: [
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
          import('./features/procurement/procurement-page.component').then(
            (m) => m.ProcurementPageComponent
          )
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports-page.component').then((m) => m.ReportsPageComponent)
      },
      {
        path: 'admin/wallets',
        canActivate: [authGuard],
        data: { role: 'admin' },
        loadComponent: () =>
          import('./features/admin/wallets-admin/wallets-admin-page.component').then(
            (m) => m.WalletsAdminPageComponent
          )
      },
      {
        path: 'admin/staff',
        canActivate: [authGuard],
        data: { role: 'admin' },
        loadComponent: () =>
          import('./features/admin/staff-admin/staff-admin-page.component').then(
            (m) => m.StaffAdminPageComponent
          )
      },
      {
        path: 'settings',
        canActivate: [authGuard],
        data: { role: 'admin' },
        loadComponent: () =>
          import('./features/settings/settings-page.component').then((m) => m.SettingsPageComponent)
      }
    ]
  },
  {
    path: '**',
    redirectTo: 'auth/login'
  }
];
