import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, ElementRef, HostListener, computed, inject, signal, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatOptionModule } from '@angular/material/core';
import { MatSelectModule } from '@angular/material/select';

import { CartItem, PaymentMethod, ReceiptPayload } from '../../core/models';
import { SYSTEM_BRANDING, SYSTEM_BRANDING_FULL_ADDRESS } from '../../core/system-branding';
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
  readonly branding = SYSTEM_BRANDING;
  readonly fullAddress = SYSTEM_BRANDING_FULL_ADDRESS;

  readonly searchTerm = signal('');
  readonly customerPhone = signal('');
  readonly pointsToRedeem = signal(0);
  readonly paymentMethod = signal<PaymentMethod>('Card');
  readonly message = signal('Ready for checkout.');

  @ViewChild('searchBox') searchBox?: ElementRef<HTMLInputElement>;

  readonly paymentOptions: PaymentMethod[] = ['Cash', 'Card', 'Digital'];

  readonly matchedProducts = computed(() => this.store.searchProducts(this.searchTerm()).slice(0, 16));

  readonly selectedCustomer = computed(() => this.store.getCustomerByPhone(this.customerPhone()));
  readonly recentPayments = computed(() => this.store.paymentHistory().slice(0, 12));

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

    if (result.success && result.receipt) {
      this.pointsToRedeem.set(0);
      this.printDocument(result.receipt, 'Receipt', true);
    }
  }

  generateQuotation(): void {
    const cart = this.store.cart();
    if (cart.length === 0) {
      this.message.set('Add at least one product to generate a quotation.');
      return;
    }

    const customer = this.selectedCustomer();
    const totals = this.totals();
    const timestamp = new Date().toISOString();
    const transactionId = `qt-${Date.now().toString().slice(-8)}`;

    const lineItems = cart.map((line) => {
      const computed = this.computeLineTotals(line);
      return {
        productId: line.productId,
        productName: line.name,
        sku: line.sku,
        quantity: line.quantity,
        unitPrice: this.roundMoney(line.price),
        discount: computed.discount,
        tax: computed.tax,
        lineTotal: computed.total
      };
    });

    const quotation: ReceiptPayload = {
      transactionId,
      timestamp,
      paymentMethod: this.paymentMethod(),
      customerName: customer?.name ?? 'Walk-in Customer',
      customerPhone: customer?.phone ?? this.customerPhone().trim(),
      totals,
      lineItems
    };

    this.printDocument(quotation, 'Quotation', false);
    this.message.set(`Quotation generated: ${transactionId}.`);
  }

  private printDocument(receipt: ReceiptPayload, documentType: 'Receipt' | 'Quotation', includePaymentMethod: boolean): void {
    try {
      const normalized = this.normalizeReceiptForPrint(receipt);
      const popup = window.open('', '_blank', 'width=430,height=720');
      if (!popup) {
        this.message.set(`${documentType} popup was blocked. Enable popups to print.`);
        return;
      }

      const rowsHtml = normalized.lineItems.length === 0
        ? '<tr><td colspan="4">No line items captured.</td></tr>'
        : normalized.lineItems
          .map((line) => `
            <tr>
              <td>
                <strong>${this.escapeHtml(line.productName)}</strong>
                <small>${this.escapeHtml(line.sku)}</small>
              </td>
              <td>${line.quantity}</td>
              <td>$${line.unitPrice.toFixed(2)}</td>
              <td>$${line.lineTotal.toFixed(2)}</td>
            </tr>`)
          .join('');

      const html = `
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8" />
          <title>${this.escapeHtml(documentType)} ${this.escapeHtml(normalized.transactionId)}</title>
          <style>
            :root { color-scheme: light; }
            body { margin: 0; font-family: 'IBM Plex Sans', Arial, sans-serif; color: #102441; }
            .receipt { width: 320px; margin: 0 auto; padding: 12px; }
            .head { display: grid; grid-template-columns: 46px 1fr; gap: 8px; align-items: center; }
            .head img { width: 46px; height: 46px; object-fit: contain; border: 1px solid #d7dce8; border-radius: 8px; padding: 3px; }
            .head h1 { margin: 0; font-size: 13px; }
            .head p { margin: 2px 0 0; font-size: 10px; color: #445777; }
            .meta { margin-top: 10px; border-top: 1px dashed #adb9cf; border-bottom: 1px dashed #adb9cf; padding: 8px 0; font-size: 11px; }
            .meta p { margin: 2px 0; }
            table { width: 100%; border-collapse: collapse; margin-top: 8px; font-size: 11px; }
            th, td { border-bottom: 1px solid #e1e7f2; padding: 6px 4px; text-align: left; vertical-align: top; }
            td:nth-child(2), td:nth-child(3), td:nth-child(4), th:nth-child(2), th:nth-child(3), th:nth-child(4) { text-align: right; white-space: nowrap; }
            td small { display: block; color: #5d7397; }
            .totals { margin-top: 10px; font-size: 11px; }
            .totals p { margin: 3px 0; display: flex; justify-content: space-between; }
            .totals p strong { font-size: 12px; }
            .foot { margin-top: 10px; border-top: 1px dashed #adb9cf; padding-top: 7px; font-size: 10px; color: #445777; text-align: center; }
            @media print {
              body { print-color-adjust: exact; -webkit-print-color-adjust: exact; }
              .receipt { width: auto; margin: 0; padding: 6mm; }
            }
          </style>
        </head>
        <body>
          <article class="receipt">
            <header class="head">
              <img src="${this.branding.logoPath}" alt="${this.escapeHtml(this.branding.name)} logo" />
              <div>
                <h1>${this.escapeHtml(this.branding.name)}</h1>
                <p>${this.escapeHtml(this.fullAddress)}</p>
                <p>${this.escapeHtml(this.branding.email)} | ${this.escapeHtml(this.branding.phone)}</p>
              </div>
            </header>

            <section class="meta">
              <p><strong>${this.escapeHtml(documentType)}:</strong> ${this.escapeHtml(normalized.transactionId)}</p>
              <p><strong>Date:</strong> ${this.escapeHtml(normalized.timestamp)}</p>
              ${includePaymentMethod ? `<p><strong>Payment:</strong> ${this.escapeHtml(normalized.paymentMethod)}</p>` : ''}
              <p><strong>Customer:</strong> ${this.escapeHtml(normalized.customerName)}</p>
              <p><strong>Phone:</strong> ${this.escapeHtml(normalized.customerPhone)}</p>
            </section>

            <table>
              <thead>
                <tr>
                  <th>Product</th>
                  <th>Qty</th>
                  <th>Price</th>
                  <th>Total</th>
                </tr>
              </thead>
              <tbody>
                ${rowsHtml}
              </tbody>
            </table>

            <section class="totals">
              <p><span>Subtotal</span><span>$${normalized.totals.subtotal.toFixed(2)}</span></p>
              <p><span>Tax</span><span>$${normalized.totals.tax.toFixed(2)}</span></p>
              <p><span>Discount</span><span>-$${normalized.totals.discount.toFixed(2)}</span></p>
              <p><span>Points Redeemed</span><span>${normalized.totals.pointsRedeemed}</span></p>
              <p><strong>Grand Total</strong><strong>$${normalized.totals.total.toFixed(2)}</strong></p>
              <p><span>Points Earned</span><span>${normalized.totals.pointsEarned}</span></p>
            </section>

            <footer class="foot">
              Thank you for shopping with us.
            </footer>
          </article>
        </body>
        </html>`;

      popup.document.open();
      popup.document.write(html);
      popup.document.close();

      let printTriggered = false;
      const triggerPrint = () => {
        if (printTriggered) {
          return;
        }

        printTriggered = true;
        popup.focus();
        popup.print();
      };

      popup.onload = triggerPrint;
      window.setTimeout(triggerPrint, 650);
    } catch {
      this.message.set(`Failed to generate ${documentType.toLowerCase()}. Please retry.`);
    }
  }

  private normalizeReceiptForPrint(receipt: ReceiptPayload): {
    transactionId: string;
    timestamp: string;
    paymentMethod: string;
    customerName: string;
    customerPhone: string;
    totals: {
      subtotal: number;
      tax: number;
      discount: number;
      pointsRedeemed: number;
      total: number;
      pointsEarned: number;
    };
    lineItems: Array<{
      productName: string;
      sku: string;
      quantity: number;
      unitPrice: number;
      lineTotal: number;
    }>;
  } {
    const safeLineItems = Array.isArray(receipt.lineItems) ? receipt.lineItems : [];
    const safeTotals = receipt.totals ?? {
      subtotal: 0,
      tax: 0,
      discount: 0,
      pointsRedeemed: 0,
      total: 0,
      pointsEarned: 0
    };

    return {
      transactionId: String(receipt.transactionId || `doc-${Date.now()}`),
      timestamp: new Date(receipt.timestamp || new Date().toISOString()).toLocaleString(),
      paymentMethod: String(receipt.paymentMethod || 'N/A'),
      customerName: String(receipt.customerName || 'Walk-in Customer'),
      customerPhone: String(receipt.customerPhone || 'N/A'),
      totals: {
        subtotal: this.safeMoney(safeTotals.subtotal),
        tax: this.safeMoney(safeTotals.tax),
        discount: this.safeMoney(safeTotals.discount),
        pointsRedeemed: this.safeInteger(safeTotals.pointsRedeemed),
        total: this.safeMoney(safeTotals.total),
        pointsEarned: this.safeInteger(safeTotals.pointsEarned)
      },
      lineItems: safeLineItems.map((line) => ({
        productName: String(line.productName || 'Unnamed Product'),
        sku: String(line.sku || 'N/A'),
        quantity: this.safeInteger(line.quantity, 1),
        unitPrice: this.safeMoney(line.unitPrice),
        lineTotal: this.safeMoney(line.lineTotal)
      }))
    };
  }

  private escapeHtml(value: string): string {
    return String(value)
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }

  private computeLineTotals(line: CartItem): { discount: number; tax: number; total: number } {
    const baseSubtotal = line.price * line.quantity;
    let discount = 0;

    if (line.promo.type === 'discount') {
      discount = baseSubtotal * (line.promo.value / 100);
    }

    if (line.promo.type === 'bogo') {
      discount = Math.floor(line.quantity / 2) * line.price;
    }

    const taxableSubtotal = Math.max(0, baseSubtotal - discount);
    const tax = taxableSubtotal * line.taxRate;

    return {
      discount: this.roundMoney(discount),
      tax: this.roundMoney(tax),
      total: this.roundMoney(taxableSubtotal + tax)
    };
  }

  private roundMoney(value: number): number {
    return Math.round(value * 100) / 100;
  }

  private safeMoney(value: unknown): number {
    const amount = Number(value);
    return Number.isFinite(amount) ? this.roundMoney(amount) : 0;
  }

  private safeInteger(value: unknown, fallback = 0): number {
    const amount = Number(value);
    if (!Number.isFinite(amount)) {
      return fallback;
    }

    return Math.max(0, Math.floor(amount));
  }
}
