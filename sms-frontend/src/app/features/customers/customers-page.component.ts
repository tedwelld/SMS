import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { SmsStoreService } from '../../core/sms-store.service';
import { CustomerProfile } from '../../core/models';

@Component({
  selector: 'app-customers-page',
  imports: [
    CommonModule,
    DatePipe,
    CurrencyPipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule
  ],
  templateUrl: './customers-page.component.html',
  styleUrl: './customers-page.component.scss'
})
export class CustomersPageComponent {
  readonly store = inject(SmsStoreService);

  readonly searchTerm = signal('');
  readonly minPoints = signal(0);
  readonly minPurchases = signal(0);
  readonly newName = signal('');
  readonly newPhone = signal('');
  readonly exporting = signal(false);
  readonly editingCustomerId = signal<string | null>(null);
  readonly editName = signal('');
  readonly editPhone = signal('');
  readonly editActive = signal(true);
  readonly feedback = signal('Create and manage loyalty member profiles.');

  readonly filteredCustomers = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    const minPoints = Math.max(0, this.minPoints());
    const minPurchases = Math.max(0, this.minPurchases());

    return this.store
      .customers()
      .filter((customer) => {
        const matchesSearch = !term
          || customer.name.toLowerCase().includes(term)
          || customer.phone.includes(term);
        const matchesPoints = customer.points >= minPoints;
        const matchesPurchaseCount = customer.purchaseHistory.length >= minPurchases;
        return matchesSearch && matchesPoints && matchesPurchaseCount;
      });
  });

  readonly totalCustomerPoints = computed(() =>
    this.filteredCustomers().reduce((sum, customer) => sum + customer.points, 0)
  );

  readonly totalPurchases = computed(() =>
    this.filteredCustomers().reduce((sum, customer) => sum + customer.purchaseHistory.length, 0)
  );

  updateSearch(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  updateName(event: Event): void {
    this.newName.set((event.target as HTMLInputElement).value);
  }

  updatePhone(event: Event): void {
    this.newPhone.set((event.target as HTMLInputElement).value);
  }

  async createMember(): Promise<void> {
    const result = await this.store.addCustomer(this.newName(), this.newPhone());
    this.feedback.set(result.message);

    if (result.success) {
      this.newName.set('');
      this.newPhone.set('');
    }
  }

  beginEdit(customer: CustomerProfile): void {
    this.editingCustomerId.set(String(customer.id));
    this.editName.set(customer.name);
    this.editPhone.set(customer.phone);
    this.editActive.set(customer.isActive ?? true);
  }

  cancelEdit(): void {
    this.editingCustomerId.set(null);
  }

  async saveMember(customer: CustomerProfile): Promise<void> {
    const result = await this.store.updateCustomer(customer.id, {
      name: this.editName(),
      phone: this.editPhone(),
      email: customer.email ?? '',
      isActive: this.editActive()
    });

    this.feedback.set(result.message);
    if (result.success) {
      this.editingCustomerId.set(null);
    }
  }

  async deleteMember(customer: CustomerProfile): Promise<void> {
    const approved = window.confirm(`Delete member '${customer.name}' (${customer.phone})?`);
    if (!approved) {
      return;
    }

    const result = await this.store.deleteCustomer(customer.id);
    this.feedback.set(result.message);
  }

  async exportCustomerReport(): Promise<void> {
    this.exporting.set(true);
    try {
      const { jsPDF } = await import('jspdf');
      const doc = new jsPDF({ orientation: 'portrait', unit: 'mm', format: 'a4' });
      const customers = this.filteredCustomers();
      const generatedAt = new Date().toLocaleString();
      const pageWidth = doc.internal.pageSize.getWidth();

      doc.setFont('helvetica', 'bold');
      doc.setFontSize(14);
      doc.text('Customer Report', 14, 14);
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(9);
      doc.text(`Generated: ${generatedAt}`, 14, 20);
      doc.text(`Filter search: "${this.searchTerm() || 'none'}"`, 14, 25);
      doc.text(`Min points: ${this.minPoints()} | Min purchases: ${this.minPurchases()}`, 14, 30);
      doc.text(`Members: ${customers.length} | Combined points: ${this.totalCustomerPoints()}`, 14, 35);

      let y = 42;
      doc.setFillColor(238, 242, 248);
      doc.rect(14, y, pageWidth - 28, 8, 'F');
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(9);
      doc.text('Member', 16, y + 5.2);
      doc.text('Phone', 82, y + 5.2);
      doc.text('Points', 130, y + 5.2);
      doc.text('Purchases', 157, y + 5.2);
      doc.text('Lifetime Value', 186, y + 5.2, { align: 'right' });
      y += 8;

      for (const customer of customers) {
        const purchases = customer.purchaseHistory.length;
        const lifetimeValue = customer.purchaseHistory.reduce((sum, p) => sum + p.total, 0);

        if (y > 278) {
          doc.addPage();
          y = 18;
        }

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(8.7);
        doc.rect(14, y, pageWidth - 28, 7.2);
        doc.text(customer.name, 16, y + 4.8);
        doc.text(customer.phone, 82, y + 4.8);
        doc.text(customer.points.toString(), 130, y + 4.8);
        doc.text(purchases.toString(), 157, y + 4.8);
        doc.text(`$${lifetimeValue.toFixed(2)}`, 186, y + 4.8, { align: 'right' });
        y += 7.2;
      }

      doc.save('sms-customer-report.pdf');
      this.feedback.set('Customer report exported.');
    } finally {
      this.exporting.set(false);
    }
  }
}
