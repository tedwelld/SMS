import { CommonModule, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Vendor } from '../../core/models';

import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-procurement-page',
  imports: [CommonModule, DatePipe, FormsModule, MatButtonModule, MatCardModule],
  templateUrl: './procurement-page.component.html',
  styleUrl: './procurement-page.component.scss'
})
export class ProcurementPageComponent {
  readonly store = inject(SmsStoreService);

  readonly generatedAt = signal(new Date());
  readonly feedback = signal('Manage vendors, purchase orders, and filtered procurement reports.');
  readonly exporting = signal(false);

  readonly vendorQuery = signal('');
  readonly orderQuery = signal('');
  readonly minSuggestedQty = signal(0);
  readonly vendorFilter = signal('all');

  readonly orderCount = computed(() => this.store.draftPurchaseOrders().length);

  readonly filteredVendors = computed(() => {
    const term = this.vendorQuery().trim().toLowerCase();
    if (!term) {
      return this.store.vendors();
    }

    return this.store.vendors().filter((vendor) =>
      vendor.name.toLowerCase().includes(term)
      || vendor.contact.toLowerCase().includes(term)
      || vendor.email.toLowerCase().includes(term)
      || vendor.departments.join(' ').toLowerCase().includes(term)
    );
  });

  readonly filteredOrders = computed(() => {
    const term = this.orderQuery().trim().toLowerCase();
    const minQty = Math.max(0, this.minSuggestedQty());
    const vendorId = this.vendorFilter();

    return this.store.draftPurchaseOrders().filter((order) => {
      const vendorMatch = vendorId === 'all' || order.vendorId === vendorId;
      const queryMatch = !term
        || order.vendorName.toLowerCase().includes(term)
        || order.lines.some((line) => line.name.toLowerCase().includes(term) || line.sku.toLowerCase().includes(term));
      const qtyMatch = order.lines.some((line) => line.suggestedOrderQty >= minQty);

      return vendorMatch && queryMatch && qtyMatch;
    });
  });

  readonly createVendorName = signal('');
  readonly createVendorContact = signal('');
  readonly createVendorEmail = signal('');
  readonly createVendorLeadTime = signal(3);
  readonly createVendorDepartments = signal('Grocery, Household');

  readonly editingVendorId = signal<string | null>(null);
  readonly editVendorName = signal('');
  readonly editVendorContact = signal('');
  readonly editVendorEmail = signal('');
  readonly editVendorLeadTime = signal(3);
  readonly editVendorDepartments = signal('');

  readonly editingOrderId = signal<string | null>(null);
  readonly orderVendorId = signal('');
  readonly orderLineName = signal('');
  readonly orderLineSku = signal('');
  readonly orderLineCurrentStock = signal(0);
  readonly orderLineSuggestedQty = signal(1);

  constructor() {
    const firstVendor = this.store.vendors()[0];
    if (firstVendor) {
      this.orderVendorId.set(firstVendor.id);
    }
  }

  refreshOrders(): void {
    this.store.generateDraftPurchaseOrders();
    this.generatedAt.set(new Date());
    this.feedback.set('Draft purchase orders regenerated.');
  }

  async createVendor(): Promise<void> {
    const result = await this.store.createVendor({
      name: this.createVendorName(),
      contact: this.createVendorContact(),
      email: this.createVendorEmail(),
      leadTimeDays: this.createVendorLeadTime(),
      departments: this.parseDepartments(this.createVendorDepartments())
    });

    this.feedback.set(result.message);
    if (!result.success) {
      return;
    }

    this.createVendorName.set('');
    this.createVendorContact.set('');
    this.createVendorEmail.set('');
    this.createVendorLeadTime.set(3);
    this.createVendorDepartments.set('Grocery, Household');
  }

  beginEditVendor(vendor: Vendor): void {
    this.editingVendorId.set(vendor.id);
    this.editVendorName.set(vendor.name);
    this.editVendorContact.set(vendor.contact);
    this.editVendorEmail.set(vendor.email);
    this.editVendorLeadTime.set(vendor.leadTimeDays);
    this.editVendorDepartments.set(vendor.departments.join(', '));
  }

  cancelVendorEdit(): void {
    this.editingVendorId.set(null);
  }

  async saveVendor(vendor: Vendor): Promise<void> {
    const result = await this.store.updateVendor(vendor.id, {
      name: this.editVendorName(),
      contact: this.editVendorContact(),
      email: this.editVendorEmail(),
      leadTimeDays: this.editVendorLeadTime(),
      departments: this.parseDepartments(this.editVendorDepartments()) as Vendor['departments']
    });

    this.feedback.set(result.message);
    if (result.success) {
      this.editingVendorId.set(null);
    }
  }

  async deleteVendor(vendor: Vendor): Promise<void> {
    const approved = window.confirm(`Delete vendor '${vendor.name}' and its draft orders?`);
    if (!approved) {
      return;
    }

    const result = await this.store.deleteVendor(vendor.id);
    this.feedback.set(result.message);
  }

  beginEditOrder(orderId: string): void {
    const order = this.store.draftPurchaseOrders().find((item) => item.id === orderId);
    if (!order || order.lines.length === 0) {
      return;
    }

    const primaryLine = order.lines[0];
    this.editingOrderId.set(order.id);
    this.orderVendorId.set(order.vendorId);
    this.orderLineName.set(primaryLine.name);
    this.orderLineSku.set(primaryLine.sku);
    this.orderLineCurrentStock.set(primaryLine.currentStock);
    this.orderLineSuggestedQty.set(primaryLine.suggestedOrderQty);
  }

  cancelOrderEdit(): void {
    this.editingOrderId.set(null);
    this.clearOrderForm();
  }

  saveOrder(): void {
    const vendorId = this.orderVendorId() || this.store.vendors()[0]?.id || '';
    const lines = [
      {
        name: this.orderLineName(),
        sku: this.orderLineSku(),
        currentStock: this.orderLineCurrentStock(),
        suggestedOrderQty: this.orderLineSuggestedQty()
      }
    ];

    const editingId = this.editingOrderId();
    const result = editingId
      ? this.store.updateDraftPurchaseOrder(editingId, { vendorId, lines })
      : this.store.createDraftPurchaseOrder({ vendorId, lines });

    this.feedback.set(result.message);
    if (result.success) {
      this.editingOrderId.set(null);
      this.clearOrderForm();
    }
  }

  deleteOrder(orderId: string): void {
    const approved = window.confirm(`Delete draft order ${orderId}?`);
    if (!approved) {
      return;
    }

    const result = this.store.deleteDraftPurchaseOrder(orderId);
    this.feedback.set(result.message);
  }

  async exportProcurementReport(): Promise<void> {
    this.exporting.set(true);
    try {
      const { jsPDF } = await import('jspdf');
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const pageWidth = doc.internal.pageSize.getWidth();
      const orders = this.filteredOrders();
      const generatedAt = new Date().toLocaleString();

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(14);
      doc.text('Procurement Report', 14, 14);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(9);
      doc.text(`Generated: ${generatedAt}`, 14, 20);
      doc.text(`Vendor filter: ${this.vendorFilter() === 'all' ? 'All vendors' : this.vendorFilter()}`, 14, 25);
      doc.text(`Query: "${this.orderQuery() || 'none'}" | Min suggested qty: ${this.minSuggestedQty()}`, 14, 30);
      doc.text(`Orders in view: ${orders.length}`, 14, 35);

      let y = 42;
      doc.setFillColor(238, 242, 248);
      doc.rect(14, y, pageWidth - 28, 8, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9);
      doc.text('Vendor', 16, y + 5.2);
      doc.text('Created', 74, y + 5.2);
      doc.text('Line Item', 110, y + 5.2);
      doc.text('SKU', 147, y + 5.2);
      doc.text('Qty', 186, y + 5.2, { align: 'right' });
      y += 8;

      for (const order of orders) {
        for (const line of order.lines) {
          if (y > 278) {
            doc.addPage();
            y = 18;
          }

          doc.setFont('helvetica', 'normal');
          doc.setFontSize(8.6);
          doc.rect(14, y, pageWidth - 28, 7.2);
          doc.text(order.vendorName, 16, y + 4.8);
          doc.text(new Date(order.createdAt).toLocaleDateString(), 74, y + 4.8);
          doc.text(line.name, 110, y + 4.8);
          doc.text(line.sku, 147, y + 4.8);
          doc.text(String(line.suggestedOrderQty), 186, y + 4.8, { align: 'right' });
          y += 7.2;
        }
      }

      doc.save('sms-procurement-report.pdf');
      this.feedback.set('Procurement report exported.');
    } finally {
      this.exporting.set(false);
    }
  }

  private parseDepartments(raw: string): string[] {
    return raw
      .split(',')
      .map((item) => item.trim())
      .filter((item) => !!item);
  }

  private clearOrderForm(): void {
    this.orderLineName.set('');
    this.orderLineSku.set('');
    this.orderLineCurrentStock.set(0);
    this.orderLineSuggestedQty.set(1);
  }
}
