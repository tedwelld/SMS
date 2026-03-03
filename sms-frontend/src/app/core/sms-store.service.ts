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
  PaymentTrackingRecord,
  PaymentMethod,
  Product,
  ReceiptPayload,
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
  private readonly apiBaseUrl = 'http://localhost:5032/api';

  roles: UserRole[] = ['Store Manager', 'Cashier', 'Stock Clerk'];

  readonly activeRole = signal<UserRole>('Store Manager');
  readonly offlineMode = signal(false);

  readonly inventory = signal<Product[]>([]);
  readonly customers = signal<CustomerProfile[]>([]);
  readonly vendors = signal<Vendor[]>([]);
  readonly auditLogs = signal<AuditEntry[]>([]);
  readonly salesTrend = signal<Array<{ hour: string; sales: number }>>([]);
  readonly draftPurchaseOrders = signal<DraftPurchaseOrder[]>([]);
  readonly paymentHistory = signal<PaymentTrackingRecord[]>([]);

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

  setOfflineMode(enabled: boolean): void {
    if (enabled === this.offlineMode()) {
      return;
    }

    this.offlineMode.set(enabled);
    void this.patchSettings({ offlineMode: enabled });
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
  ): Promise<{ success: boolean; message: string; transactionId?: string; receipt?: ReceiptPayload }> {
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
        transactionId: response.transactionId,
        receipt: response.receipt
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

  async updateProductStock(
    productId: string,
    quantity: number,
    mode: 'set' | 'add'
  ): Promise<OperationResult> {
    const safeQuantity = Number.isFinite(quantity) ? Math.max(0, Math.floor(quantity)) : -1;
    if (safeQuantity < 0) {
      return { success: false, message: 'Enter a valid stock quantity.' };
    }

    try {
      await firstValueFrom(
        this.http.patch(`${this.apiBaseUrl}/products/${productId}/stock`, {
          quantity: safeQuantity,
          mode
        })
      );
      await this.refreshBootstrap();

      return {
        success: true,
        message: mode === 'set' ? 'System stock set successfully.' : 'Stock units added successfully.'
      };
    } catch (error) {
      return { success: false, message: this.apiErrorMessage(error) };
    }
  }

  async addCustomer(name: string, phone: string): Promise<OperationResult> {
    const normalizedPhone = this.normalizePhoneForApi(phone);
    if (!name.trim() || !normalizedPhone) {
      return { success: false, message: 'Name and a valid phone number are required.' };
    }

    try {
      await firstValueFrom(this.http.post(`${this.apiBaseUrl}/customers`, {
        name: name.trim(),
        phoneNumber: normalizedPhone,
        phone: normalizedPhone
      }));
      await this.refreshBootstrap();
      return { success: true, message: 'Member profile created.' };
    } catch (error) {
      return { success: false, message: this.apiErrorMessage(error) };
    }
  }

  async updateCustomer(
    customerId: number | string,
    payload: { name: string; phone: string; email?: string; isActive?: boolean }
  ): Promise<OperationResult> {
    const parsedId = Number(customerId);
    if (!Number.isFinite(parsedId) || parsedId <= 0) {
      return { success: false, message: 'Invalid customer id.' };
    }

    const normalizedPhone = this.normalizePhoneForApi(payload.phone);
    if (!payload.name.trim() || !normalizedPhone) {
      return { success: false, message: 'Name and a valid phone number are required.' };
    }

    try {
      await firstValueFrom(this.http.put(`${this.apiBaseUrl}/customers/${parsedId}`, {
        name: payload.name.trim(),
        phoneNumber: normalizedPhone,
        email: (payload.email ?? '').trim(),
        isActive: payload.isActive ?? true
      }));
      await this.refreshBootstrap();
      return { success: true, message: 'Member profile updated.' };
    } catch (error) {
      return { success: false, message: this.apiErrorMessage(error) };
    }
  }

  async deleteCustomer(customerId: number | string): Promise<OperationResult> {
    const parsedId = Number(customerId);
    if (!Number.isFinite(parsedId) || parsedId <= 0) {
      return { success: false, message: 'Invalid customer id.' };
    }

    try {
      await firstValueFrom(this.http.delete(`${this.apiBaseUrl}/customers/${parsedId}`));
      await this.refreshBootstrap();
      return { success: true, message: 'Member profile deleted.' };
    } catch (error) {
      return { success: false, message: this.apiErrorMessage(error) };
    }
  }

  generateDraftPurchaseOrders(): void {
    void this.mutateAndRefresh(() =>
      firstValueFrom(this.http.post(`${this.apiBaseUrl}/purchase-orders/drafts/regenerate`, {}))
    );
  }

  createVendor(payload: {
    name: string;
    contact: string;
    email: string;
    leadTimeDays: number;
    departments: string[];
  }): OperationResult {
    const normalizedName = payload.name.trim();
    if (!normalizedName) {
      return { success: false, message: 'Vendor name is required.' };
    }

    const vendor: Vendor = {
      id: `V-${Date.now()}`,
      name: normalizedName,
      contact: payload.contact.trim() || 'N/A',
      email: payload.email.trim() || `${normalizedName.toLowerCase().replace(/\s+/g, '.')}@vendor.local`,
      leadTimeDays: Math.max(1, Math.floor(payload.leadTimeDays || 1)),
      departments: payload.departments as Vendor['departments']
    };

    this.vendors.update((items) => [vendor, ...items]);
    return { success: true, message: 'Vendor added.' };
  }

  updateVendor(vendorId: string, patch: Partial<Vendor>): OperationResult {
    const existing = this.vendors().find((item) => item.id === vendorId);
    if (!existing) {
      return { success: false, message: 'Vendor not found.' };
    }

    const merged: Vendor = {
      ...existing,
      ...patch,
      name: (patch.name ?? existing.name).trim(),
      contact: (patch.contact ?? existing.contact).trim(),
      email: (patch.email ?? existing.email).trim(),
      leadTimeDays: Math.max(1, Math.floor(patch.leadTimeDays ?? existing.leadTimeDays))
    };

    this.vendors.update((items) => items.map((item) => (item.id === vendorId ? merged : item)));
    return { success: true, message: 'Vendor updated.' };
  }

  deleteVendor(vendorId: string): OperationResult {
    const exists = this.vendors().some((item) => item.id === vendorId);
    if (!exists) {
      return { success: false, message: 'Vendor not found.' };
    }

    this.vendors.update((items) => items.filter((item) => item.id !== vendorId));
    this.draftPurchaseOrders.update((orders) => orders.filter((order) => order.vendorId !== vendorId));
    return { success: true, message: 'Vendor deleted.' };
  }

  createDraftPurchaseOrder(payload: {
    vendorId: string;
    lines: Array<{ name: string; sku: string; currentStock: number; suggestedOrderQty: number }>;
  }): OperationResult {
    const vendor = this.vendors().find((item) => item.id === payload.vendorId);
    if (!vendor) {
      return { success: false, message: 'Vendor is required.' };
    }

    const lines = payload.lines
      .map((line, index) => ({
        productId: `${payload.vendorId}-${Date.now()}-${index + 1}`,
        name: line.name.trim(),
        sku: line.sku.trim() || `SKU-${Date.now()}-${index + 1}`,
        currentStock: Math.max(0, Math.floor(line.currentStock || 0)),
        suggestedOrderQty: Math.max(1, Math.floor(line.suggestedOrderQty || 1))
      }))
      .filter((line) => !!line.name);

    if (lines.length === 0) {
      return { success: false, message: 'At least one order line is required.' };
    }

    const order: DraftPurchaseOrder = {
      id: `PO-${Date.now()}`,
      vendorId: vendor.id,
      vendorName: vendor.name,
      createdAt: new Date().toISOString(),
      lines
    };

    this.draftPurchaseOrders.update((orders) => [order, ...orders]);
    return { success: true, message: 'Draft purchase order created.' };
  }

  updateDraftPurchaseOrder(
    orderId: string,
    payload: { vendorId: string; lines: Array<{ name: string; sku: string; currentStock: number; suggestedOrderQty: number }> }
  ): OperationResult {
    const existing = this.draftPurchaseOrders().find((item) => item.id === orderId);
    if (!existing) {
      return { success: false, message: 'Draft order not found.' };
    }

    const vendor = this.vendors().find((item) => item.id === payload.vendorId);
    if (!vendor) {
      return { success: false, message: 'Vendor is required.' };
    }

    const lines = payload.lines
      .map((line, index) => ({
        productId: existing.lines[index]?.productId ?? `${payload.vendorId}-${Date.now()}-${index + 1}`,
        name: line.name.trim(),
        sku: line.sku.trim() || `SKU-${Date.now()}-${index + 1}`,
        currentStock: Math.max(0, Math.floor(line.currentStock || 0)),
        suggestedOrderQty: Math.max(1, Math.floor(line.suggestedOrderQty || 1))
      }))
      .filter((line) => !!line.name);

    if (lines.length === 0) {
      return { success: false, message: 'At least one order line is required.' };
    }

    const updated: DraftPurchaseOrder = {
      ...existing,
      vendorId: vendor.id,
      vendorName: vendor.name,
      lines
    };

    this.draftPurchaseOrders.update((orders) => orders.map((order) => (order.id === orderId ? updated : order)));
    return { success: true, message: 'Draft purchase order updated.' };
  }

  deleteDraftPurchaseOrder(orderId: string): OperationResult {
    const exists = this.draftPurchaseOrders().some((item) => item.id === orderId);
    if (!exists) {
      return { success: false, message: 'Draft order not found.' };
    }

    this.draftPurchaseOrders.update((orders) => orders.filter((order) => order.id !== orderId));
    return { success: true, message: 'Draft purchase order deleted.' };
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
    this.paymentHistory.set(payload.recentPayments ?? []);

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

  private normalizePhoneForApi(phone: string): string {
    return String(phone || '').trim().replace(/[^\d+]/g, '');
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

    return 'Request failed. Verify the API server is running at http://localhost:5032.';
  }
}
