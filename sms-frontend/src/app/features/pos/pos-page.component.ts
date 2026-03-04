import { CommonModule, CurrencyPipe } from '@angular/common';
import { Component, ElementRef, HostListener, ViewChild, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { toDataURL } from 'qrcode';

import { AuthService } from '../../core/auth.service';
import { CartItem, PaymentMethod, PaymentTrackingRecord, ReceiptPayload } from '../../core/models';
import { SmsStoreService } from '../../core/sms-store.service';
import { SYSTEM_BRANDING, SYSTEM_BRANDING_FULL_ADDRESS } from '../../core/system-branding';

@Component({
  selector: 'app-pos-page',
  imports: [CommonModule, CurrencyPipe, MatButtonModule, MatCardModule],
  templateUrl: './pos-page.component.html',
  styleUrl: './pos-page.component.scss'
})
export class PosPageComponent {
  readonly store = inject(SmsStoreService);
  readonly auth = inject(AuthService);
  readonly branding = SYSTEM_BRANDING;
  readonly fullAddress = SYSTEM_BRANDING_FULL_ADDRESS;

  readonly searchTerm = signal('');
  readonly customerPhone = signal('');
  readonly pointsToRedeem = signal(0);
  readonly paymentMethod = signal<PaymentMethod>('Card');
  readonly message = signal('Ready for checkout.');
  readonly lastGeneratedReceipt = signal<ReceiptPayload | null>(null);

  readonly salesQuery = signal('');
  readonly salesMethodFilter = signal<PaymentMethod | 'all'>('all');
  readonly salesFromDate = signal('');
  readonly salesToDate = signal('');
  readonly loadingSalesLedger = signal(false);

  @ViewChild('searchBox') searchBox?: ElementRef<HTMLInputElement>;

  readonly paymentOptions: PaymentMethod[] = ['Cash', 'Card', 'Digital'];

  readonly matchedProducts = computed(() => this.store.searchProducts(this.searchTerm()).slice(0, 16));
  readonly selectedCustomer = computed(() => this.store.getCustomerByPhone(this.customerPhone()));
  readonly canViewAllSales = computed(() => this.auth.role === 'admin');

  readonly trackedPayments = computed(() => {
    const query = this.salesQuery().trim().toLowerCase();
    const method = this.salesMethodFilter();
    const from = this.parseDateFilter(this.salesFromDate());
    const to = this.parseDateFilter(this.salesToDate());

    const base = this.store.paymentHistory();
    const scoped = this.canViewAllSales() ? base : base.slice(0, 16);

    return scoped
      .filter((payment) =>
        method === 'all' ? true : payment.paymentMethod === method
      )
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

  readonly totals = computed(() => {
    const customer = this.selectedCustomer();
    const allowedPoints = customer ? customer.points : 0;
    const requested = Math.max(0, this.pointsToRedeem());
    const safePoints = Math.min(allowedPoints, requested);
    return this.store.getCartTotals(safePoints);
  });

  constructor() {
    void this.refreshSalesLedger();
  }

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

  updateSalesQuery(event: Event): void {
    this.salesQuery.set((event.target as HTMLInputElement).value);
  }

  setSalesMethodFilter(method: string): void {
    if (method === 'Cash' || method === 'Card' || method === 'Digital' || method === 'all') {
      this.salesMethodFilter.set(method);
    }
  }

  updateSalesFromDate(event: Event): void {
    this.salesFromDate.set((event.target as HTMLInputElement).value);
  }

  updateSalesToDate(event: Event): void {
    this.salesToDate.set((event.target as HTMLInputElement).value);
  }

  async refreshSalesLedger(): Promise<void> {
    this.loadingSalesLedger.set(true);
    await this.store.refreshPaymentHistory(this.canViewAllSales() ? 500 : 120, {
      from: this.salesFromDate(),
      to: this.salesToDate(),
      method: this.salesMethodFilter(),
      query: this.salesQuery()
    });
    this.loadingSalesLedger.set(false);
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

  canPreviewReceipt(): boolean {
    return this.store.cart().length > 0 || this.lastGeneratedReceipt() !== null;
  }

  canPrintReceipt(): boolean {
    return this.store.cart().length > 0 || this.lastGeneratedReceipt() !== null;
  }

  async previewReceipt(): Promise<void> {
    const payload = this.store.cart().length > 0
      ? this.buildCartReceiptPayload()
      : this.lastGeneratedReceipt();

    if (!payload) {
      this.message.set('Add items or load a receipt before previewing.');
      return;
    }

    const ok = await this.printDocument(payload, 'Receipt Preview', true, false);
    if (ok) {
      this.message.set('Receipt preview opened.');
    }
  }

  async printReceipt(): Promise<void> {
    const payload = this.store.cart().length > 0
      ? this.buildCartReceiptPayload()
      : this.lastGeneratedReceipt();

    if (!payload) {
      this.message.set('Add items or load a receipt before printing.');
      return;
    }

    const ok = await this.printDocument(payload, 'Receipt', true, true);
    if (ok) {
      this.message.set('Receipt sent to printer.');
    }
  }

  async reprintReceipt(transactionId: string): Promise<void> {
    if (!this.canViewAllSales()) {
      this.message.set('Only admins can reprint historical receipts.');
      return;
    }

    const receipt = await this.store.getReceiptByTransactionId(transactionId);
    if (!receipt) {
      this.message.set(`Receipt not found for transaction ${transactionId}.`);
      return;
    }

    this.lastGeneratedReceipt.set(receipt);
    const ok = await this.printDocument(receipt, 'Receipt Reprint', true, true);
    if (ok) {
      this.message.set(`Receipt ${transactionId} reprinted.`);
    }
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
      this.lastGeneratedReceipt.set(result.receipt);
      await this.printDocument(result.receipt, 'Receipt', true, true);
      await this.refreshSalesLedger();
    }
  }

  async generateQuotation(): Promise<void> {
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

    const ok = await this.printDocument(quotation, 'Quotation', false, false);
    if (ok) {
      this.message.set(`Quotation generated: ${transactionId}.`);
    }
  }

  private buildCartReceiptPayload(): ReceiptPayload {
    const cart = this.store.cart();
    const customer = this.selectedCustomer();
    const totals = this.totals();

    return {
      transactionId: `tmp-${Date.now().toString().slice(-8)}`,
      timestamp: new Date().toISOString(),
      paymentMethod: this.paymentMethod(),
      customerName: customer?.name ?? 'Walk-in Customer',
      customerPhone: customer?.phone ?? this.customerPhone().trim(),
      totals,
      lineItems: cart.map((line) => {
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
      })
    };
  }

  private async printDocument(
    receipt: ReceiptPayload,
    documentType: 'Receipt' | 'Quotation' | 'Receipt Preview' | 'Receipt Reprint',
    includePaymentMethod: boolean,
    autoPrint: boolean
  ): Promise<boolean> {
    let popup: Window | null = null;
    try {
      const normalized = this.normalizeReceiptForPrint(receipt);
      popup = window.open('', '_blank', 'width=430,height=760');
      if (!popup) {
        this.message.set(`${documentType} popup was blocked. Enable popups to print.`);
        return false;
      }

      const verificationToken = includePaymentMethod
        ? await this.resolveReceiptQrToken(normalized.transactionId, normalized.qrToken)
        : '';
      const verifyUrl = verificationToken
        ? this.buildVerificationUrl(normalized.transactionId, verificationToken)
        : '';
      const qrImage = verifyUrl ? await this.generateQrCodeDataUrl(verifyUrl) : '';
      const isCompletedCheckout = normalized.transactionId.toLowerCase().startsWith('tx-');

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

      const qrBlock = qrImage
        ? `
          <section class="verification">
            <div>
              <p><strong>Verify Receipt</strong></p>
              <small>${isCompletedCheckout
                ? 'Scan to validate this transaction.'
                : 'Scan to check if this maps to a completed transaction.'}</small>
            </div>
            <img src="${qrImage}" alt="Receipt verification QR code" />
          </section>
          <p class="verify-url">${this.escapeHtml(verifyUrl)}</p>`
        : '';

      const html = `
        <!doctype html>
        <html>
        <head>
          <meta charset="utf-8" />
          <title>${this.escapeHtml(documentType)} ${this.escapeHtml(normalized.transactionId)}</title>
          <style>
            :root { color-scheme: light; }
            body { margin: 0; font-family: 'IBM Plex Sans', Arial, sans-serif; color: #102441; }
            .receipt { width: 330px; margin: 0 auto; padding: 12px; }
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
            .verification { margin-top: 10px; border: 1px solid #e1e7f2; border-radius: 8px; padding: 7px; display: flex; justify-content: space-between; align-items: center; gap: 6px; }
            .verification img { width: 70px; height: 70px; }
            .verify-url { margin: 4px 0 0; font-size: 9px; color: #50617f; word-break: break-all; }
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

            ${qrBlock}

            <footer class="foot">
              Thank you for shopping with us.
            </footer>
          </article>
        </body>
        </html>`;

      popup.document.open();
      popup.document.write(html);
      popup.document.close();

      if (!autoPrint) {
        return true;
      }

      let printTriggered = false;
      const triggerPrint = () => {
        if (printTriggered) {
          return;
        }

        printTriggered = true;
        popup?.focus();
        popup?.print();
      };

      popup.onload = triggerPrint;
      window.setTimeout(triggerPrint, 650);
      return true;
    } catch {
      if (popup && !popup.closed) {
        popup.close();
      }
      this.message.set(`Failed to generate ${documentType.toLowerCase()}. Please retry.`);
      return false;
    }
  }

  private normalizeReceiptForPrint(receipt: ReceiptPayload): {
    transactionId: string;
    timestamp: string;
    paymentMethod: string;
    customerName: string;
    customerPhone: string;
    qrToken: string;
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
      qrToken: String(receipt.qrToken || ''),
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

  private buildVerificationUrl(transactionId: string, token: string): string {
    const origin = typeof window !== 'undefined' ? window.location.origin : '';
    if (origin) {
      return `${origin}/receipt-verify?transactionId=${encodeURIComponent(transactionId)}&token=${encodeURIComponent(token)}`;
    }

    return `${this.store.apiBaseUrl}/receipts/${encodeURIComponent(transactionId)}/verify?token=${encodeURIComponent(token)}`;
  }

  private async resolveReceiptQrToken(transactionId: string, currentToken: string): Promise<string> {
    const normalizedCurrent = String(currentToken || '').trim();
    if (normalizedCurrent) {
      return normalizedCurrent;
    }

    const generated = await this.store.getReceiptQrToken(transactionId);
    if (generated) {
      return generated;
    }

    const fallbackSeed = String(transactionId || '')
      .toLowerCase()
      .replace(/[^a-z0-9-]/g, '')
      .slice(0, 28);
    return `offline-${fallbackSeed || Date.now().toString()}`;
  }

  private async generateQrCodeDataUrl(value: string): Promise<string> {
    try {
      return await toDataURL(value, {
        errorCorrectionLevel: 'M',
        margin: 1,
        width: 160
      });
    } catch {
      return '';
    }
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

  private parseDateFilter(value: string): Date | null {
    const raw = String(value || '').trim();
    if (!raw) {
      return null;
    }

    const parsed = new Date(raw);
    return Number.isNaN(parsed.getTime()) ? null : parsed;
  }
}
