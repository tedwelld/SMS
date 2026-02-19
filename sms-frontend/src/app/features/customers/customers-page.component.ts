import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { SmsStoreService } from '../../core/sms-store.service';

@Component({
  selector: 'app-customers-page',
  imports: [
    CommonModule,
    DatePipe,
    CurrencyPipe,
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
  readonly newName = signal('');
  readonly newPhone = signal('');
  readonly feedback = signal('Create and manage loyalty member profiles.');

  readonly filteredCustomers = computed(() => {
    const term = this.searchTerm().toLowerCase().trim();
    if (!term) {
      return this.store.customers();
    }

    return this.store
      .customers()
      .filter((customer) => customer.name.toLowerCase().includes(term) || customer.phone.includes(term));
  });

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
}
