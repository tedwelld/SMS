import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';

import { AuthService } from '../../core/auth.service';
import { Product, PromotionType } from '../../core/models';
import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-inventory-page',
  imports: [
    CommonModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTabsModule
  ],
  templateUrl: './inventory-page.component.html',
  styleUrl: './inventory-page.component.scss'
})
export class InventoryPageComponent {
  readonly store = inject(SmsStoreService);
  readonly auth = inject(AuthService);

  readonly stockEntryMessage = signal('');
  readonly canManuallyAdjustStock = computed(() => this.auth.role === 'admin');

  readonly totalVariance = computed(() =>
    this.store
      .shrinkageReport()
      .reduce((sum, item) => sum + Math.abs(item.variance), 0)
  );

  onPhysicalCountChange(productId: string, event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    if (Number.isFinite(value) && value >= 0) {
      this.store.updatePhysicalCount(productId, Math.floor(value));
    }
  }

  async onManualStockEntry(
    product: Product,
    input: HTMLInputElement,
    mode: 'set' | 'add'
  ): Promise<void> {
    if (!this.canManuallyAdjustStock()) {
      this.stockEntryMessage.set('Only admins can manually enter stock.');
      return;
    }

    const amount = Number(input.value);
    if (!Number.isFinite(amount) || amount < 0) {
      this.stockEntryMessage.set('Enter a valid stock quantity.');
      return;
    }

    const result = await this.store.updateProductStock(product.id, amount, mode);
    this.stockEntryMessage.set(result.message);

    if (result.success) {
      input.value = '';
    }
  }

  applyPromotion(productId: string, rawType: string, rawValue: string): void {
    const type = this.toPromotionType(rawType);
    const value = Number(rawValue);
    this.store.updatePromotion(productId, type, Number.isFinite(value) ? value : 0);
  }

  variance(product: Product): number {
    return product.physicalCount - product.stock;
  }

  daysToExpiry(product: Product): number | null {
    if (!product.expiryDate) {
      return null;
    }

    const diff = new Date(product.expiryDate).getTime() - new Date().getTime();
    return Math.max(0, Math.ceil(diff / (1000 * 60 * 60 * 24)));
  }

  private toPromotionType(raw: string): PromotionType {
    if (raw === 'discount' || raw === 'bogo') {
      return raw;
    }
    return 'none';
  }
}
