import { CommonModule, DatePipe } from '@angular/common';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatOptionModule } from '@angular/material/core';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatToolbarModule } from '@angular/material/toolbar';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';

import { AuthService } from './core/auth.service';
import { UserRole } from './core/models';
import { SmsStoreService } from './core/sms-store.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  roles: Array<'admin' | 'staff'>;
}

@Component({
  selector: 'app-root',
  imports: [
    CommonModule,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatListModule,
    MatOptionModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSidenavModule,
    MatSlideToggleModule,
    MatToolbarModule
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnDestroy {
  readonly store = inject(SmsStoreService);
  readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly breakpointObserver = inject(BreakpointObserver);

  readonly sidebarOpen = signal(false);
  readonly isMobile = signal(false);
  readonly now = signal(new Date());
  readonly isAuthRoute = signal(false);

  readonly navItems: NavItem[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard', roles: ['admin', 'staff'] },
    { path: '/pos', label: 'POS', icon: 'point_of_sale', roles: ['admin', 'staff'] },
    { path: '/inventory', label: 'Inventory', icon: 'inventory_2', roles: ['admin', 'staff'] },
    { path: '/customers', label: 'Customers', icon: 'group', roles: ['admin', 'staff'] },
    { path: '/procurement', label: 'Procurement', icon: 'local_shipping', roles: ['admin', 'staff'] },
    { path: '/reports', label: 'Reports', icon: 'analytics', roles: ['admin', 'staff'] },
    { path: '/admin/wallets', label: 'Wallet Admin', icon: 'account_balance_wallet', roles: ['admin'] },
    { path: '/admin/staff', label: 'Staff Admin', icon: 'admin_panel_settings', roles: ['admin'] }
  ];

  private readonly timerId = window.setInterval(() => {
    this.now.set(new Date());
  }, 30000);

  private readonly breakpointSub = this.breakpointObserver
    .observe([Breakpoints.Handset])
    .subscribe((state) => {
      this.isMobile.set(state.matches);
      if (!state.matches) {
        this.sidebarOpen.set(false);
      }
    });

  private readonly routeSub = this.router.events
    .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
    .subscribe(() => this.refreshRouteState());

  constructor() {
    this.refreshRouteState();
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  canAccessNav(item: NavItem): boolean {
    return item.roles.includes(this.auth.role);
  }

  logout(): void {
    this.auth.logout();
    this.store.clearCart();
    void this.router.navigateByUrl('/auth/login');
  }

  setRole(role: string): void {
    if (role === 'Store Manager' || role === 'Cashier' || role === 'Stock Clerk') {
      this.store.setActiveRole(role as UserRole);
    }
  }

  ngOnDestroy(): void {
    window.clearInterval(this.timerId);
    this.breakpointSub.unsubscribe();
    this.routeSub.unsubscribe();
  }

  private refreshRouteState(): void {
    this.isAuthRoute.set(this.router.url.startsWith('/auth'));
  }
}
