import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';

import { AuthService } from '../../core/auth.service';
import { Product, PromotionType } from '../../core/models';
import { SmsStoreService } from '../../core/sms-store.service';

interface InventoryReportRow {
  id: string;
  name: string;
  sku: string;
  department: string;
  currentStock: number;
  minStock: number;
  unitsSold: number;
  salesAmount: number;
  arrivalDate?: string;
  expiryDate?: string;
}

interface ProductSalesSummary {
  unitsSold: number;
  salesAmount: number;
}

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
export class InventoryPageComponent implements OnInit {
  readonly store = inject(SmsStoreService);
  readonly auth = inject(AuthService);

  readonly stockEntryMessage = signal('');
  readonly canManuallyAdjustStock = computed(() => this.auth.role === 'admin');

  readonly totalVariance = computed(() =>
    this.store
      .shrinkageReport()
      .reduce((sum, item) => sum + Math.abs(item.variance), 0)
  );

  readonly addName = signal('');
  readonly addSku = signal('');
  readonly addDepartment = signal('Grocery');
  readonly addPrice = signal(0);
  readonly addStock = signal(0);
  readonly addMinStock = signal(0);
  readonly addTaxRate = signal(0);
  readonly addStaple = signal(true);
  readonly addArrivalDate = signal(new Date().toISOString().slice(0, 10));
  readonly addExpiryDate = signal('');

  readonly editProductId = signal('');
  readonly editName = signal('');
  readonly editSku = signal('');
  readonly editDepartment = signal('Grocery');
  readonly editPrice = signal(0);
  readonly editStock = signal(0);
  readonly editMinStock = signal(0);
  readonly editTaxRate = signal(0);
  readonly editStaple = signal(true);
  readonly editArrivalDate = signal('');
  readonly editExpiryDate = signal('');
  readonly editSearchTerm = signal('');

  readonly reportMessage = signal('Use the filters to generate inventory reports by sales, quantity, expiry, and arrival date.');
  readonly minSalesFilter = signal(0);
  readonly minQuantityFilter = signal(0);
  readonly expiryFromFilter = signal('');
  readonly expiryToFilter = signal('');
  readonly arrivalFromFilter = signal('');
  readonly arrivalToFilter = signal('');
  readonly exportingReport = signal(false);

  readonly departments = ['Grocery', 'Dairy', 'Electronics', 'Household', 'Produce'];

  readonly salesByProduct = computed<Map<string, ProductSalesSummary>>(() => {
    const map = new Map<string, ProductSalesSummary>();
    for (const payment of this.store.paymentHistory()) {
      for (const line of payment.lineItems) {
        const current = map.get(line.productId) ?? { unitsSold: 0, salesAmount: 0 };
        current.unitsSold += Math.max(0, line.quantity);
        current.salesAmount += Math.max(0, line.lineTotal);
        map.set(line.productId, current);
      }
    }

    return map;
  });

  readonly reportRows = computed<InventoryReportRow[]>(() => {
    const salesMap = this.salesByProduct();

    return this.store.inventory().map((item) => {
      const sales = salesMap.get(item.id) ?? { unitsSold: 0, salesAmount: 0 };
      return {
        id: item.id,
        name: item.name,
        sku: item.sku,
        department: item.department,
        currentStock: item.stock,
        minStock: item.minStock,
        unitsSold: sales.unitsSold,
        salesAmount: sales.salesAmount,
        arrivalDate: item.arrivalDate,
        expiryDate: item.expiryDate
      };
    });
  });

  readonly filteredEditableProducts = computed(() => {
    const query = this.editSearchTerm().trim().toLowerCase();
    if (!query) {
      return this.store.inventory();
    }

    return this.store.inventory().filter((item) =>
      item.name.toLowerCase().includes(query) || item.sku.toLowerCase().includes(query)
    );
  });

  readonly filteredReportRows = computed(() => {
    const minSales = Math.max(0, this.minSalesFilter());
    const minQty = Math.max(0, this.minQuantityFilter());
    const expiryFrom = this.expiryFromFilter();
    const expiryTo = this.expiryToFilter();
    const arrivalFrom = this.arrivalFromFilter();
    const arrivalTo = this.arrivalToFilter();

    return this.reportRows()
      .filter((row) => row.salesAmount >= minSales)
      .filter((row) => row.currentStock >= minQty)
      .filter((row) => this.inDateRange(row.expiryDate, expiryFrom, expiryTo))
      .filter((row) => this.inDateRange(row.arrivalDate, arrivalFrom, arrivalTo))
      .sort((a, b) => b.salesAmount - a.salesAmount || b.unitsSold - a.unitsSold || a.name.localeCompare(b.name));
  });

  readonly reportTotals = computed(() => {
    const rows = this.filteredReportRows();
    return {
      products: rows.length,
      quantityOnHand: rows.reduce((sum, row) => sum + row.currentStock, 0),
      unitsSold: rows.reduce((sum, row) => sum + row.unitsSold, 0),
      salesAmount: rows.reduce((sum, row) => sum + row.salesAmount, 0)
    };
  });

  async ngOnInit(): Promise<void> {
    await this.store.refreshPaymentHistory(500);
    this.ensureEditorSelection();
  }

  onPhysicalCountChange(productId: string, event: Event): void {
    if (!this.canManuallyAdjustStock()) {
      this.stockEntryMessage.set('Only admins can update physical counts.');
      return;
    }

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
    if (!this.canManuallyAdjustStock()) {
      this.stockEntryMessage.set('Only admins can update product pricing and promotions.');
      return;
    }

    const type = this.toPromotionType(rawType);
    const value = Number(rawValue);
    this.store.updatePromotion(productId, type, Number.isFinite(value) ? value : 0);
  }

  async addProduct(): Promise<void> {
    if (!this.canManuallyAdjustStock()) {
      this.stockEntryMessage.set('Only admins can add products.');
      return;
    }

    const result = await this.store.addProduct({
      name: this.addName(),
      sku: this.addSku(),
      department: this.addDepartment(),
      price: this.addPrice(),
      stock: this.addStock(),
      minStock: this.addMinStock(),
      taxRate: this.addTaxRate(),
      staple: this.addStaple(),
      arrivalDate: this.addArrivalDate(),
      expiryDate: this.addExpiryDate()
    });

    this.stockEntryMessage.set(result.message);
    if (result.success) {
      this.addName.set('');
      this.addSku.set('');
      this.addPrice.set(0);
      this.addStock.set(0);
      this.addMinStock.set(0);
      this.addTaxRate.set(0);
      this.addStaple.set(true);
      this.addArrivalDate.set(new Date().toISOString().slice(0, 10));
      this.addExpiryDate.set('');
      this.ensureEditorSelection();
    }
  }

  async removeProduct(item: Product): Promise<void> {
    if (!this.canManuallyAdjustStock()) {
      this.stockEntryMessage.set('Only admins can remove products.');
      return;
    }

    const approved = window.confirm(`Remove product '${item.name}' (${item.sku}) from the system?`);
    if (!approved) {
      return;
    }

    const result = await this.store.deleteProduct(item.id);
    this.stockEntryMessage.set(result.message);
    if (result.success) {
      this.ensureEditorSelection();
    }
  }

  onEditProductSelectionChange(productId: string): void {
    this.editProductId.set(productId);
    this.loadEditorFromSelectedProduct();
  }

  updateEditSearch(event: Event): void {
    this.editSearchTerm.set((event.target as HTMLInputElement).value);

    const filtered = this.filteredEditableProducts();
    if (filtered.length === 0) {
      return;
    }

    const current = this.editProductId();
    if (!filtered.some((item) => item.id === current)) {
      this.editProductId.set(filtered[0].id);
      this.loadEditorFromSelectedProduct();
    }
  }

  async saveProductEdits(): Promise<void> {
    if (!this.canManuallyAdjustStock()) {
      this.stockEntryMessage.set('Only admins can edit products.');
      return;
    }

    const productId = this.editProductId();
    if (!productId) {
      this.stockEntryMessage.set('Select a product to edit.');
      return;
    }

    const result = await this.store.updateProduct(productId, {
      name: this.editName(),
      sku: this.editSku(),
      department: this.editDepartment(),
      price: this.editPrice(),
      stock: this.editStock(),
      minStock: this.editMinStock(),
      taxRate: this.editTaxRate(),
      staple: this.editStaple(),
      arrivalDate: this.editArrivalDate(),
      expiryDate: this.editExpiryDate()
    });

    this.stockEntryMessage.set(result.message);
    if (result.success) {
      this.loadEditorFromSelectedProduct();
    }
  }

  async refreshSalesData(): Promise<void> {
    await this.store.refreshPaymentHistory(500);
    this.reportMessage.set('Sales data refreshed from latest payment records.');
  }

  resetReportFilters(): void {
    this.minSalesFilter.set(0);
    this.minQuantityFilter.set(0);
    this.expiryFromFilter.set('');
    this.expiryToFilter.set('');
    this.arrivalFromFilter.set('');
    this.arrivalToFilter.set('');
    this.reportMessage.set('Inventory report filters reset.');
  }

  async exportInventoryReport(): Promise<void> {
    this.exportingReport.set(true);
    try {
      const { jsPDF } = await import('jspdf');
      const doc = new jsPDF({ orientation: 'landscape', unit: 'mm', format: 'a4' });
      const rows = this.filteredReportRows();
      const pageWidth = doc.internal.pageSize.getWidth();

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(14);
      doc.text('Inventory Report', 14, 14);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(9);
      doc.text(`Generated: ${new Date().toLocaleString()}`, 14, 20);
      doc.text(
        `Filters -> Min sales: ${this.minSalesFilter()} | Min qty: ${this.minQuantityFilter()} | Expiry: ${this.expiryFromFilter() || '-'} to ${this.expiryToFilter() || '-'} | Arrival: ${this.arrivalFromFilter() || '-'} to ${this.arrivalToFilter() || '-'}`,
        14,
        25
      );
      doc.text(
        `Products: ${this.reportTotals().products} | Qty on hand: ${this.reportTotals().quantityOnHand} | Units sold: ${this.reportTotals().unitsSold} | Sales: $${this.reportTotals().salesAmount.toFixed(2)}`,
        14,
        30
      );

      let y = 36;
      doc.setFillColor(238, 242, 248);
      doc.rect(14, y, pageWidth - 28, 8, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(8.5);
      doc.text('Product', 16, y + 5.2);
      doc.text('SKU', 72, y + 5.2);
      doc.text('Dept', 96, y + 5.2);
      doc.text('Qty', 124, y + 5.2);
      doc.text('Min', 138, y + 5.2);
      doc.text('Units Sold', 152, y + 5.2);
      doc.text('Sales', 178, y + 5.2);
      doc.text('Arrival', 206, y + 5.2);
      doc.text('Expiry', 236, y + 5.2);
      y += 8;

      for (const row of rows) {
        if (y > 196) {
          doc.addPage();
          y = 18;
        }

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8.2);
        doc.rect(14, y, pageWidth - 28, 7.2);
        doc.text(row.name, 16, y + 4.8, { maxWidth: 54 });
        doc.text(row.sku, 72, y + 4.8);
        doc.text(row.department, 96, y + 4.8);
        doc.text(String(row.currentStock), 124, y + 4.8);
        doc.text(String(row.minStock), 138, y + 4.8);
        doc.text(String(row.unitsSold), 152, y + 4.8);
        doc.text(`$${row.salesAmount.toFixed(2)}`, 178, y + 4.8);
        doc.text(row.arrivalDate || '-', 206, y + 4.8);
        doc.text(row.expiryDate || '-', 236, y + 4.8);
        y += 7.2;
      }

      doc.save('sms-inventory-report.pdf');
      this.reportMessage.set('Inventory report exported.');
    } finally {
      this.exportingReport.set(false);
    }
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

  updateNumeric(signalSetter: (value: number) => void, event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    signalSetter(Number.isFinite(value) ? value : 0);
  }

  private inDateRange(rawDate: string | undefined, fromRaw: string, toRaw: string): boolean {
    const from = this.parseDate(fromRaw);
    const to = this.parseDate(toRaw);
    if (!from && !to) {
      return true;
    }

    const value = this.parseDate(rawDate ?? '');
    if (!value) {
      return false;
    }

    if (from && value < from) {
      return false;
    }

    if (to && value > to) {
      return false;
    }

    return true;
  }

  private parseDate(raw: string): Date | null {
    if (!raw?.trim()) {
      return null;
    }

    const date = new Date(raw);
    return Number.isNaN(date.getTime()) ? null : date;
  }

  private toPromotionType(raw: string): PromotionType {
    if (raw === 'discount' || raw === 'bogo') {
      return raw;
    }
    return 'none';
  }

  private ensureEditorSelection(): void {
    if (!this.canManuallyAdjustStock()) {
      return;
    }

    const products = this.store.inventory();
    if (products.length === 0) {
      this.editProductId.set('');
      return;
    }

    const current = this.editProductId();
    if (!products.some((item) => item.id === current)) {
      this.editProductId.set(products[0].id);
    }

    this.loadEditorFromSelectedProduct();
  }

  private loadEditorFromSelectedProduct(): void {
    const selected = this.store.inventory().find((item) => item.id === this.editProductId());
    if (!selected) {
      return;
    }

    this.editName.set(selected.name);
    this.editSku.set(selected.sku);
    this.editDepartment.set(selected.department);
    this.editPrice.set(selected.price);
    this.editStock.set(selected.stock);
    this.editMinStock.set(selected.minStock);
    this.editTaxRate.set(selected.taxRate);
    this.editStaple.set(selected.staple);
    this.editArrivalDate.set(selected.arrivalDate ?? '');
    this.editExpiryDate.set(selected.expiryDate ?? '');
  }
}
