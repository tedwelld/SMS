import { CommonModule, DatePipe } from '@angular/common';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
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

import { UserRole } from './core/models';
import { SmsStoreService } from './core/sms-store.service';

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
  private readonly breakpointObserver = inject(BreakpointObserver);

  readonly sidebarOpen = signal(false);
  readonly isMobile = signal(false);
  readonly now = signal(new Date());

  readonly navItems = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/pos', label: 'POS', icon: 'point_of_sale' },
    { path: '/inventory', label: 'Inventory', icon: 'inventory_2' },
    { path: '/customers', label: 'Customers', icon: 'group' },
    { path: '/procurement', label: 'Procurement', icon: 'local_shipping' },
    { path: '/reports', label: 'Reports', icon: 'analytics' }
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

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  setRole(role: string): void {
    if (role === 'Store Manager' || role === 'Cashier' || role === 'Stock Clerk') {
      this.store.setActiveRole(role as UserRole);
    }
  }

  ngOnDestroy(): void {
    window.clearInterval(this.timerId);
    this.breakpointSub.unsubscribe();
  }
}
