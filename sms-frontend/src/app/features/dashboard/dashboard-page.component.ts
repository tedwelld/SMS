import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';

import { SmsStoreService } from '../../core/sms-store.service';

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

  readonly now = new Date();
  readonly totalRevenue = computed(() => this.store.eodReport().total);
  readonly openSuspended = computed(() => (this.store.suspendedTransaction() ? 1 : 0));

  readonly roleCards = [
    {
      role: 'Store Manager',
      summary: 'Overrides prices, manages shifts, and reviews full-store performance metrics.'
    },
    {
      role: 'Cashier',
      summary: 'Manages checkouts, customer returns, and end-of-day cash reconciliation.'
    },
    {
      role: 'Stock Clerk',
      summary: 'Records shipment intake and keeps system stock aligned with physical counts.'
    }
  ];
}
