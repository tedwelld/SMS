import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, ElementRef, HostListener, computed, inject, signal, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatOptionModule } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';

import { PaymentMethod } from '../../core/models';
import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-pos-page',
  imports: [
    CommonModule,
    CurrencyPipe,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatOptionModule,
    MatSelectModule
  ],
  templateUrl: './pos-page.component.html',
  styleUrl: './pos-page.component.scss'
})
export class PosPageComponent {
  readonly store = inject(SmsStoreService);

  readonly searchTerm = signal('');
  readonly customerPhone = signal('');
  readonly pointsToRedeem = signal(0);
  readonly paymentMethod = signal<PaymentMethod>('Card');
  readonly message = signal('Ready for checkout.');

  @ViewChild('searchBox') searchBox?: ElementRef<HTMLInputElement>;

  readonly paymentOptions: PaymentMethod[] = ['Cash', 'Card', 'Digital'];

  readonly matchedProducts = computed(() => this.store.searchProducts(this.searchTerm()).slice(0, 8));

  readonly selectedCustomer = computed(() => this.store.getCustomerByPhone(this.customerPhone()));

  readonly totals = computed(() => {
    const customer = this.selectedCustomer();
    const allowedPoints = customer ? customer.points : 0;
    const requested = Math.max(0, this.pointsToRedeem());
    const safePoints = Math.min(allowedPoints, requested);
    return this.store.getCartTotals(safePoints);
  });

  @HostListener('window:keydown', ['$event'])
  onKeyboardShortcut(event: KeyboardEvent): void {
    if (event.ctrlKey && event.key.toLowerCase() === 'k') {
      event.preventDefault();
      this.searchBox?.nativeElement.focus();
    }

    if (event.key === 'F2') {
      event.preventDefault();
      this.holdTransaction();
    }

    if (event.key === 'F3') {
      event.preventDefault();
      this.resumeTransaction();
    }
  }

  updateSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  updateCustomerPhone(event: Event): void {
    this.customerPhone.set((event.target as HTMLInputElement).value);
  }

  updateRedeemPoints(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.pointsToRedeem.set(Number.isFinite(value) ? Math.max(0, Math.floor(value)) : 0);
  }

  setPaymentMethod(method: string): void {
    if (method === 'Cash' || method === 'Card' || method === 'Digital') {
      this.paymentMethod.set(method);
    }
  }

  holdTransaction(): void {
    const held = this.store.holdTransaction();
    this.message.set(held ? 'Transaction suspended. Press F3 to resume.' : 'Cart is empty. Nothing to suspend.');
  }

  resumeTransaction(): void {
    const resumed = this.store.resumeTransaction();
    this.message.set(resumed ? 'Suspended transaction restored.' : 'No suspended transaction found.');
  }

  async checkout(): Promise<void> {
    const result = await this.store.checkout(
      this.paymentMethod(),
      this.customerPhone(),
      this.selectedCustomer() ? this.pointsToRedeem() : 0
    );

    this.message.set(result.message);

    if (result.success) {
      this.pointsToRedeem.set(0);
    }
  }
}
