import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import {
  AuditEntry,
  BootstrapPayload,
  CartItem,
  CartTotals,
  CheckoutResponse,
  CustomerProfile,
  DraftPurchaseOrder,
  EodReport,
  OperationResult,
  PaymentMethod,
  Product,
  PromotionType,
  ShrinkageReportRow,
  UserRole,
  Vendor
} from './models';

const POINT_VALUE = 0.05;
const EXPIRY_WARNING_DAYS = 14;

@Injectable({ providedIn: 'root' })
export class SmsStoreService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = 'http://localhost:3000/api';

  roles: UserRole[] = ['Store Manager', 'Cashier', 'Stock Clerk'];

  readonly activeRole = signal<UserRole>('Store Manager');
  readonly offlineMode = signal(false);

  readonly inventory = signal<Product[]>([]);
  readonly customers = signal<CustomerProfile[]>([]);
  readonly vendors = signal<Vendor[]>([]);
  readonly auditLogs = signal<AuditEntry[]>([]);
  readonly salesTrend = signal<Array<{ hour: string; sales: number }>>([]);
  readonly draftPurchaseOrders = signal<DraftPurchaseOrder[]>([]);

  private readonly eod = signal<EodReport>({
    cash: 0,
    card: 0,
    digital: 0,
    total: 0,
    transactions: 0
  });

  private readonly shrinkage = signal<ShrinkageReportRow[]>([]);

  readonly cart = signal<CartItem[]>([]);
  readonly suspendedTransaction = signal<CartItem[] | null>(null);

  readonly loading = signal(false);
  readonly lastError = signal<string | null>(null);

  readonly lowStockItems = computed(() =>
    this.inventory().filter((item) => item.stock <= item.minStock)
  );

  readonly expiringSoonItems = computed(() => {
    const now = new Date();

    return this.inventory().filter((item) => {
      if (!item.expiryDate) {
        return false;
      }

      const expires = new Date(item.expiryDate);
      const diffDays = (expires.getTime() - now.getTime()) / (1000 * 60 * 60 * 24);
      return diffDays >= 0 && diffDays <= EXPIRY_WARNING_DAYS;
    });
  });

  constructor() {
    void this.refreshBootstrap();
  }

  async refreshBootstrap(): Promise<void> {
    this.loading.set(true);

    try {
      const payload = await firstValueFrom(this.http.get<BootstrapPayload>(`${this.apiBaseUrl}/bootstrap`));
      this.applyBootstrap(payload);
      this.lastError.set(null);
    } catch (error) {
      this.lastError.set(this.apiErrorMessage(error));
    } finally {
      this.loading.set(false);
    }
  }

  setActiveRole(role: UserRole): void {
    this.activeRole.set(role);
    void this.patchSettings({ activeRole: role });
  }

  toggleOfflineMode(): void {
    const next = !this.offlineMode();
    this.offlineMode.set(next);
    void this.patchSettings({ offlineMode: next });
  }

  searchProducts(term: string): Product[] {
    const normalized = term.trim().toLowerCase();
    if (!normalized) {
      return this.inventory();
    }

    return this.inventory().filter(
      (item) =>
        item.name.toLowerCase().includes(normalized) || item.sku.toLowerCase().includes(normalized)
    );
  }

  addToCart(productId: string): void {
    const product = this.inventory().find((item) => item.id === productId);
    if (!product || product.stock <= 0) {
      return;
    }

    this.cart.update((items) => {
      const existing = items.find((item) => item.productId === product.id);
      if (existing) {
        return items.map((item) =>
          item.productId === product.id
            ? { ...item, quantity: Math.min(item.quantity + 1, product.stock) }
            : item
        );
      }

      return [
        ...items,
        {
          productId: product.id,
          name: product.name,
          sku: product.sku,
          price: product.price,
          quantity: 1,
          taxRate: product.taxRate,
          promo: product.promo
        }
      ];
    });
  }

  increaseCartQuantity(productId: string): void {
    const product = this.inventory().find((item) => item.id === productId);
    if (!product) {
      return;
    }

    this.cart.update((items) =>
      items.map((item) =>
        item.productId === productId
          ? { ...item, quantity: Math.min(item.quantity + 1, product.stock) }
          : item
      )
    );
  }

  decreaseCartQuantity(productId: string): void {
    this.cart.update((items) =>
      items
        .map((item) =>
          item.productId === productId ? { ...item, quantity: Math.max(0, item.quantity - 1) } : item
        )
        .filter((item) => item.quantity > 0)
    );
  }

  removeFromCart(productId: string): void {
    this.cart.update((items) => items.filter((item) => item.productId !== productId));
  }

  clearCart(): void {
    this.cart.set([]);
  }

  holdTransaction(): boolean {
    if (this.cart().length === 0) {
      return false;
    }

    this.suspendedTransaction.set(this.cart());
    this.cart.set([]);
    return true;
  }

  resumeTransaction(): boolean {
    const suspended = this.suspendedTransaction();
    if (!suspended) {
      return false;
    }

    this.cart.set(suspended);
    this.suspendedTransaction.set(null);
    return true;
  }

  getCartTotals(pointsToRedeem: number): CartTotals {
    const lineTotals = this.cart().map((item) => this.getLineTotals(item));
    const subtotal = lineTotals.reduce((sum, line) => sum + line.taxableSubtotal, 0);
    const tax = lineTotals.reduce((sum, line) => sum + line.tax, 0);
    const discount = lineTotals.reduce((sum, line) => sum + line.discount, 0);
    const gross = subtotal + tax;

    const redeemablePoints = Math.max(0, Math.floor(pointsToRedeem));
    const maxPointsByTotal = Math.floor(gross / POINT_VALUE);
    const pointsRedeemed = Math.min(redeemablePoints, maxPointsByTotal);
    const total = Math.max(0, gross - pointsRedeemed * POINT_VALUE);
    const pointsEarned = Math.floor(total / 12);

    return {
      subtotal,
      tax,
      discount,
      pointsRedeemed,
      total,
      pointsEarned
    };
  }

  async checkout(
    method: PaymentMethod,
    customerPhone: string,
    pointsToRedeem: number
  ): Promise<{ success: boolean; message: string; transactionId?: string }> {
    if (this.cart().length === 0) {
      return { success: false, message: 'Cart is empty.' };
    }

    try {
      const response = await firstValueFrom(
        this.http.post<CheckoutResponse>(`${this.apiBaseUrl}/checkout`, {
          cart: this.cart().map((line) => ({ productId: line.productId, quantity: line.quantity })),
          paymentMethod: method,
          customerPhone,
          pointsToRedeem,
          userRole: this.activeRole()
        })
      );

      this.applyBootstrap(response.bootstrap);
      this.clearCart();

      return {
        success: true,
        message: response.message,
        transactionId: response.transactionId
      };
    } catch (error) {
      return {
        success: false,
        message: this.apiErrorMessage(error)
      };
    }
  }

  updatePromotion(productId: string, type: PromotionType, value: number): void {
    void this.mutateAndRefresh(() =>
      firstValueFrom(
        this.http.patch(`${this.apiBaseUrl}/products/${productId}/promotion`, {
          type,
          value
        })
      )
    );
  }

  updatePhysicalCount(productId: string, count: number): void {
    void this.mutateAndRefresh(() =>
      firstValueFrom(
        this.http.patch(`${this.apiBaseUrl}/products/${productId}/physical-count`, {
          physicalCount: count
        })
      )
    );
  }

  async addCustomer(name: string, phone: string): Promise<OperationResult> {
    try {
      await firstValueFrom(this.http.post(`${this.apiBaseUrl}/customers`, { name, phone }));
      await this.refreshBootstrap();
      return { success: true, message: 'Member profile created.' };
    } catch (error) {
      return { success: false, message: this.apiErrorMessage(error) };
    }
  }

  generateDraftPurchaseOrders(): void {
    void this.mutateAndRefresh(() =>
      firstValueFrom(this.http.post(`${this.apiBaseUrl}/purchase-orders/drafts/regenerate`, {}))
    );
  }

  eodReport(): EodReport {
    return this.eod();
  }

  shrinkageReport(): ShrinkageReportRow[] {
    return this.shrinkage();
  }

  getCustomerByPhone(phone: string): CustomerProfile | undefined {
    const normalized = this.normalizePhone(phone);
    if (!normalized) {
      return undefined;
    }

    return this.customers().find((customer) => customer.phone === normalized);
  }

  getRecentAuditEntries(limit = 8): AuditEntry[] {
    return this.auditLogs().slice(0, limit);
  }

  private applyBootstrap(payload: BootstrapPayload): void {
    this.roles = payload.settings.roles;
    this.activeRole.set(payload.settings.activeRole);
    this.offlineMode.set(payload.settings.offlineMode);

    this.inventory.set(payload.products);
    this.customers.set(payload.customers);
    this.vendors.set(payload.vendors);
    this.draftPurchaseOrders.set(payload.draftPurchaseOrders);
    this.salesTrend.set(payload.salesTrend);
    this.auditLogs.set(payload.auditLogs);

    this.eod.set(payload.reports.eod);
    this.shrinkage.set(payload.reports.shrinkage);
  }

  private async patchSettings(patch: {
    activeRole?: UserRole;
    offlineMode?: boolean;
  }): Promise<void> {
    try {
      await firstValueFrom(this.http.patch(`${this.apiBaseUrl}/settings`, patch));
      await this.refreshBootstrap();
    } catch (error) {
      this.lastError.set(this.apiErrorMessage(error));
    }
  }

  private async mutateAndRefresh(request: () => Promise<unknown>): Promise<void> {
    try {
      await request();
      await this.refreshBootstrap();
    } catch (error) {
      this.lastError.set(this.apiErrorMessage(error));
    }
  }

  private getLineTotals(item: CartItem): {
    taxableSubtotal: number;
    discount: number;
    tax: number;
  } {
    const baseSubtotal = item.price * item.quantity;

    let discount = 0;

    if (item.promo.type === 'discount') {
      discount = baseSubtotal * (item.promo.value / 100);
    }

    if (item.promo.type === 'bogo') {
      discount = Math.floor(item.quantity / 2) * item.price;
    }

    const taxableSubtotal = Math.max(0, baseSubtotal - discount);
    const tax = taxableSubtotal * item.taxRate;

    return {
      taxableSubtotal,
      discount,
      tax
    };
  }

  private normalizePhone(phone: string): string {
    return String(phone || '').replace(/\D/g, '');
  }

  private apiErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error?.message === 'string') {
        return error.error.message;
      }

      if (typeof error.message === 'string') {
        return error.message;
      }
    }

    return 'Request failed. Verify the API server is running at http://localhost:3000.';
  }
}
