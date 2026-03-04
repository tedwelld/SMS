import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { AuthService } from '../../core/auth.service';
import { PaymentMethod, StaffCashUp } from '../../core/models';
import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-cash-ups-page',
  imports: [CommonModule, CurrencyPipe, DatePipe, MatButtonModule, MatCardModule],
  templateUrl: './cash-ups-page.component.html',
  styleUrl: './cash-ups-page.component.scss'
})
export class CashUpsPageComponent {
  readonly store = inject(SmsStoreService);
  readonly auth = inject(AuthService);

  readonly salesQuery = signal('');
  readonly salesMethodFilter = signal<PaymentMethod | 'all'>('all');
  readonly salesFromDate = signal('');
  readonly salesToDate = signal('');
  readonly loadingSalesLedger = signal(false);

  readonly cashUpFromDate = signal('');
  readonly cashUpToDate = signal('');
  readonly cashUpStaffSearch = signal('');
  readonly loadingCashUps = signal(false);
  readonly submittingCashUp = signal(false);
  readonly statusMessage = signal('Ready.');

  readonly isAdmin = computed(() => this.auth.role === 'admin');
  readonly staffUserId = computed(() => this.auth.session()?.staffUserId ?? 0);
  readonly staffDisplayName = computed(() => this.auth.session()?.displayName ?? 'Staff User');

  readonly trackedPayments = computed(() => {
    const query = this.salesQuery().trim().toLowerCase();
    const method = this.salesMethodFilter();
    const from = this.parseDateFilter(this.salesFromDate());
    const to = this.parseDateFilter(this.salesToDate());

    return this.store.paymentHistory()
      .filter((payment) => method === 'all' || payment.paymentMethod === method)
      .filter((payment) => {
        const timestamp = new Date(payment.timestamp);
        if (Number.isNaN(timestamp.getTime())) {
          return false;
        }
        if (from && timestamp < from) {
          return false;
        }
        if (to) {
          const end = new Date(to);
          end.setHours(23, 59, 59, 999);
          if (timestamp > end) {
            return false;
          }
        }
        return true;
      })
      .filter((payment) => {
        if (!query) {
          return true;
        }

        return payment.transactionId.toLowerCase().includes(query)
          || payment.customerName.toLowerCase().includes(query)
          || payment.customerPhone.toLowerCase().includes(query);
      });
  });

  readonly salesLedgerTotals = computed(() => ({
    transactions: this.trackedPayments().length,
    grossSales: this.trackedPayments().reduce((sum, payment) => sum + payment.total, 0)
  }));

  readonly filteredCashUps = computed(() => {
    const from = this.parseDateFilter(this.cashUpFromDate());
    const to = this.parseDateFilter(this.cashUpToDate());
    const staffId = this.staffUserId();
    const staffSearch = this.cashUpStaffSearch().trim().toLowerCase();

    return this.store.cashUps()
      .filter((entry) => this.isAdmin() || entry.staffUserId === staffId)
      .filter((entry) => {
        if (!staffSearch) {
          return true;
        }

        return entry.staffName.toLowerCase().includes(staffSearch);
      })
      .filter((entry) => {
        const date = this.parseDateFilter(entry.businessDate);
        if (!date) {
          return false;
        }
        if (from && date < from) {
          return false;
        }
        if (to && date > to) {
          return false;
        }
        return true;
      });
  });

  readonly cashUpTotals = computed(() => ({
    count: this.filteredCashUps().length,
    total: this.filteredCashUps().reduce((sum, row) => sum + row.total, 0)
  }));

  readonly latestMyCashUp = computed<StaffCashUp | null>(() => {
    const mine = this.filteredCashUps().filter((entry) => entry.staffUserId === this.staffUserId());
    return mine.length > 0 ? mine[0] : null;
  });

  async ngOnInit(): Promise<void> {
    await Promise.all([
      this.refreshSalesLedger(),
      this.refreshCashUps()
    ]);
  }

  updateSalesQuery(event: Event): void {
    this.salesQuery.set((event.target as HTMLInputElement).value);
  }

  setSalesMethodFilter(method: string): void {
    if (method === 'Cash' || method === 'Card' || method === 'EcoCash' || method === 'all') {
      this.salesMethodFilter.set(method);
    }
  }

  updateSalesFromDate(event: Event): void {
    this.salesFromDate.set((event.target as HTMLInputElement).value);
  }

  updateSalesToDate(event: Event): void {
    this.salesToDate.set((event.target as HTMLInputElement).value);
  }

  updateCashUpFromDate(event: Event): void {
    this.cashUpFromDate.set((event.target as HTMLInputElement).value);
  }

  updateCashUpToDate(event: Event): void {
    this.cashUpToDate.set((event.target as HTMLInputElement).value);
  }

  updateCashUpStaffSearch(event: Event): void {
    this.cashUpStaffSearch.set((event.target as HTMLInputElement).value);
  }

  async refreshSalesLedger(): Promise<void> {
    if (!this.isAdmin()) {
      return;
    }

    this.loadingSalesLedger.set(true);
    await this.store.refreshPaymentHistory(500, {
      from: this.salesFromDate(),
      to: this.salesToDate(),
      method: this.salesMethodFilter(),
      query: this.salesQuery()
    });
    this.loadingSalesLedger.set(false);
  }

  async refreshCashUps(): Promise<void> {
    this.loadingCashUps.set(true);
    await this.store.refreshCashUps({
      from: this.cashUpFromDate(),
      to: this.cashUpToDate(),
      staffUserId: this.isAdmin() ? undefined : this.staffUserId()
    });
    this.loadingCashUps.set(false);
  }

  async submitCashUp(): Promise<void> {
    const staffUserId = this.staffUserId();
    if (!Number.isFinite(staffUserId) || staffUserId <= 0) {
      this.statusMessage.set('Your account is missing a staff id. Cannot submit cash up.');
      return;
    }

    this.submittingCashUp.set(true);
    const result = await this.store.submitDailyCashUp({
      staffUserId,
      staffName: this.staffDisplayName(),
      businessDate: new Date().toISOString().slice(0, 10)
    });
    this.submittingCashUp.set(false);

    this.statusMessage.set(result.message);
    if (result.success) {
      await this.refreshCashUps();
    }
  }

  private parseDateFilter(value: string): Date | null {
    const raw = String(value || '').trim();
    if (!raw) {
      return null;
    }

    const parsed = new Date(raw);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }
}
