import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-procurement-page',
  imports: [CommonModule, DatePipe, MatButtonModule, MatCardModule],
  templateUrl: './procurement-page.component.html',
  styleUrl: './procurement-page.component.scss'
})
export class ProcurementPageComponent {
  readonly store = inject(SmsStoreService);

  readonly generatedAt = signal(new Date());
  readonly orderCount = computed(() => this.store.draftPurchaseOrders().length);

  refreshOrders(): void {
    this.store.generateDraftPurchaseOrders();
    this.generatedAt.set(new Date());
  }
}
