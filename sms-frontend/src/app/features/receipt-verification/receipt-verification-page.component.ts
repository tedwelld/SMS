import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { ReceiptVerificationResult } from '../../core/models';
import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-receipt-verification-page',
  imports: [CommonModule, CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './receipt-verification-page.component.html',
  styleUrl: './receipt-verification-page.component.scss'
})
export class ReceiptVerificationPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly store = inject(SmsStoreService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly result = signal<ReceiptVerificationResult | null>(null);
  readonly checkedAt = signal<Date | null>(null);
  readonly transactionId = signal('');

  async ngOnInit(): Promise<void> {
    const transactionId = this.route.snapshot.queryParamMap.get('transactionId') ?? '';
    const token = this.route.snapshot.queryParamMap.get('token') ?? '';

    const normalizedTransactionId = transactionId.trim();
    const normalizedToken = token.trim();
    this.transactionId.set(normalizedTransactionId);

    if (!normalizedTransactionId || !normalizedToken) {
      this.loading.set(false);
      this.error.set('Invalid verification link. Missing transaction ID or token.');
      return;
    }

    const payload = await this.store.verifyReceipt(normalizedTransactionId, normalizedToken);
    this.loading.set(false);
    this.checkedAt.set(new Date());

    if (!payload) {
      this.error.set('Verification failed. Please try again or contact support.');
      return;
    }

    this.result.set(payload);
  }
}
