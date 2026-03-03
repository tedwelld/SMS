import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';

import { SmsStoreService } from '../../core/sms-store.service';

interface DashboardStatPoint {
  key: string;
  label: string;
  value: number;
  format: 'currency' | 'number';
  color: string;
  icon: string;
  route: string;
  description: string;
}

@Component({
  selector: 'app-dashboard-page',
  imports: [
    CommonModule,
    RouterLink,
    CurrencyPipe,
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatTabsModule
  ],
  templateUrl: './dashboard-page.component.html',
  styleUrl: './dashboard-page.component.scss'
})
export class DashboardPageComponent {
  readonly store = inject(SmsStoreService);
  private readonly usd = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0
  });

  readonly now = new Date();
  readonly quarterEnd = computed(() => this.getQuarterEndDate(new Date()));
  readonly daysRemainingInQuarter = computed(() => {
    const today = new Date();
    const quarterEnd = this.quarterEnd();
    const diffMs = quarterEnd.getTime() - today.getTime();
    return Math.max(1, Math.ceil(diffMs / (1000 * 60 * 60 * 24)));
  });

  readonly totalRevenue = computed(() => this.store.eodReport().total);
  readonly totalTransactions = computed(() => this.store.eodReport().transactions);
  readonly totalCustomers = computed(() => this.store.customers().length);
  readonly openSuspended = computed(() => (this.store.suspendedTransaction() ? 1 : 0));
  readonly averageBasketValue = computed(() => {
    const transactions = this.totalTransactions();
    return transactions > 0 ? this.totalRevenue() / transactions : 0;
  });

  readonly projectedQuarterRevenue = computed(() => this.totalRevenue() * this.daysRemainingInQuarter());
  readonly projectedQuarterTransactions = computed(() => this.totalTransactions() * this.daysRemainingInQuarter());
  readonly projectedQuarterCustomers = computed(() => {
    const current = this.totalCustomers();
    const dailyGrowth = Math.max(0.2, current / Math.max(1, 90 - this.daysRemainingInQuarter()));
    return Math.round(current + dailyGrowth * this.daysRemainingInQuarter());
  });

  readonly statSeries = computed<DashboardStatPoint[]>(() => [
    {
      key: 'revenue',
      label: 'End-of-day Revenue',
      value: Math.max(this.totalRevenue(), 0),
      format: 'currency',
      color: '#1b76ff',
      icon: 'pi pi-wallet',
      route: '/reports',
      description: 'Live total from all payment channels.'
    },
    {
      key: 'transactions',
      label: 'Transactions',
      value: Math.max(this.totalTransactions(), 0),
      format: 'number',
      color: '#0d9f6e',
      icon: 'pi pi-shopping-bag',
      route: '/pos',
      description: 'Completed checkouts in the current reporting cycle.'
    },
    {
      key: 'customers',
      label: 'Customers',
      value: Math.max(this.totalCustomers(), 0),
      format: 'number',
      color: '#2c65d7',
      icon: 'pi pi-users',
      route: '/customers',
      description: 'Registered loyalty members and active customer base.'
    },
    {
      key: 'low-stock',
      label: 'Low Stock SKUs',
      value: Math.max(this.store.lowStockItems().length, 0),
      format: 'number',
      color: '#f29f33',
      icon: 'pi pi-exclamation-triangle',
      route: '/inventory',
      description: 'Items at or below minimum stock threshold.'
    },
    {
      key: 'expiry',
      label: 'Expiry Alerts',
      value: Math.max(this.store.expiringSoonItems().length, 0),
      format: 'number',
      color: '#b94b2a',
      icon: 'pi pi-clock',
      route: '/inventory',
      description: 'Products approaching expiry date soon.'
    },
    {
      key: 'suspended',
      label: 'Suspended Transactions',
      value: Math.max(this.openSuspended(), 0),
      format: 'number',
      color: '#6f58d8',
      icon: 'pi pi-pause-circle',
      route: '/pos',
      description: 'Transactions currently on hold in POS.'
    },
    {
      key: 'avg-basket',
      label: 'Average Basket',
      value: Math.max(this.averageBasketValue(), 0),
      format: 'currency',
      color: '#0f8291',
      icon: 'pi pi-calculator',
      route: '/reports',
      description: 'Average value per completed transaction.'
    }
  ]);
  readonly maxStatValue = computed(() =>
    Math.max(...this.statSeries().map((item) => item.value), 1)
  );
  readonly totalStatValue = computed(() =>
    this.statSeries().reduce((sum, item) => sum + item.value, 0)
  );
  readonly statPieGradient = computed(() => {
    const items = this.statSeries();
    const total = this.totalStatValue();

    if (total <= 0) {
      return 'conic-gradient(#d5ddea 0 100%)';
    }

    let cursor = 0;
    const slices = items.map((item) => {
      const percent = (item.value / total) * 100;
      const start = cursor;
      cursor += percent;
      return `${item.color} ${start.toFixed(2)}% ${cursor.toFixed(2)}%`;
    });

    return `conic-gradient(${slices.join(', ')})`;
  });

  readonly roleCards = [
    {
      role: 'Store Manager',
      icon: 'pi pi-briefcase',
      summary: 'Overrides prices, manages shifts, and reviews full-store performance metrics.',
      route: '/admin/staff'
    },
    {
      role: 'Cashier',
      icon: 'pi pi-credit-card',
      summary: 'Manages checkouts, customer returns, and end-of-day cash reconciliation.',
      route: '/pos'
    },
    {
      role: 'Stock Clerk',
      icon: 'pi pi-box',
      summary: 'Records shipment intake and keeps system stock aligned with physical counts.',
      route: '/inventory'
    }
  ];

  readonly operationsCards = [
    {
      title: 'Reliability',
      icon: 'pi pi-wifi',
      summary: 'Offline mode keeps checkout active during network interruptions.',
      route: '/settings'
    },
    {
      title: 'Data Integrity',
      icon: 'pi pi-shield',
      summary: 'Every transaction and override is timestamped with role-level audit visibility.',
      route: '/reports'
    },
    {
      title: 'Efficiency',
      icon: 'pi pi-bolt',
      summary: 'POS shortcuts support keyboard-only execution for faster cashier throughput.',
      route: '/pos'
    }
  ];

  barWidth(value: number): string {
    return `${Math.max(8, (value / this.maxStatValue()) * 100)}%`;
  }

  formatStat(point: DashboardStatPoint): string {
    if (point.format === 'currency') {
      return this.usd.format(point.value);
    }

    return point.value.toLocaleString('en-US');
  }

  statPercentage(point: DashboardStatPoint): string {
    const total = this.totalStatValue();
    if (total <= 0) {
      return '0.0%';
    }

    return `${((point.value / total) * 100).toFixed(1)}%`;
  }

  private getQuarterEndDate(date: Date): Date {
    const month = date.getMonth();
    const quarterEndMonth = Math.floor(month / 3) * 3 + 2;
    return new Date(date.getFullYear(), quarterEndMonth + 1, 0);
  }
}
