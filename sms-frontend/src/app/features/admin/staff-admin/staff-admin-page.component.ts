import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { StaffUserItem } from '../../../core/wallet-admin.models';
import { WalletAdminService } from '../../../core/wallet-admin.service';

@Component({
  selector: 'app-staff-admin-page',
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './staff-admin-page.component.html',
  styleUrl: './staff-admin-page.component.scss'
})
export class StaffAdminPageComponent {
  private readonly walletAdmin = inject(WalletAdminService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly staffUsers = signal<StaffUserItem[]>([]);
  readonly query = signal('');
  readonly statusOverrides = signal<Record<number, 'active' | 'inactive' | 'suspended'>>({});

  readonly filteredStaffUsers = computed(() => {
    const term = this.query().trim().toLowerCase();
    if (!term) {
      return this.staffUsers();
    }

    return this.staffUsers().filter((user) =>
      user.name.toLowerCase().includes(term)
      || user.username.toLowerCase().includes(term)
      || user.email.toLowerCase().includes(term)
      || user.role.toLowerCase().includes(term)
      || this.getStatus(user).includes(term)
    );
  });

  readonly form = this.fb.group({
    username: ['', [Validators.required]],
    name: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(6)]],
    role: ['staff', [Validators.required]]
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.success.set(null);
    try {
      const users = await this.walletAdmin.getStaffUsers();
      const overrides = this.statusOverrides();
      this.staffUsers.set(users.map((item) => ({
        ...item,
        status: overrides[item.id] ?? (item.isActive ? 'active' : 'inactive')
      })));
    } catch {
      this.error.set('Failed to load staff users.');
    } finally {
      this.loading.set(false);
    }
  }

  async create(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.success.set(null);
    try {
      const payload = this.form.getRawValue() as {
        username: string;
        name: string;
        email: string;
        password: string;
        role: string;
      };
      await this.walletAdmin.createStaff(payload);
      this.form.reset({ role: 'staff' });
      this.success.set('User created successfully.');
      await this.load();
    } catch (error) {
      this.error.set(this.apiErrorMessage(error, 'Failed to create staff user.'));
    } finally {
      this.submitting.set(false);
    }
  }

  async setStatus(user: StaffUserItem, status: 'active' | 'inactive' | 'suspended'): Promise<void> {
    try {
      this.error.set(null);
      this.success.set(null);
      await this.walletAdmin.updateStaffStatus(user.id, status);
      this.statusOverrides.update((items) => ({
        ...items,
        [user.id]: status
      }));
      this.success.set(`${user.name} is now ${status}.`);
      await this.load();
    } catch (error) {
      this.error.set(this.apiErrorMessage(error, 'Failed to update staff status.'));
    }
  }

  async updateRole(user: StaffUserItem, role: string): Promise<void> {
    if (role !== 'admin' && role !== 'staff' && role !== 'manager') {
      this.error.set('Invalid role selected.');
      return;
    }

    try {
      this.error.set(null);
      this.success.set(null);
      await this.walletAdmin.updateStaffRole(user.id, role);
      this.success.set(`${user.name} role updated to ${role}.`);
      await this.load();
    } catch (error) {
      this.error.set(this.apiErrorMessage(error, 'Failed to update staff role.'));
    }
  }

  async deleteUser(user: StaffUserItem): Promise<void> {
    const approved = window.confirm(`Delete user '${user.name}' (${user.username})? This cannot be undone.`);
    if (!approved) {
      return;
    }

    try {
      this.error.set(null);
      this.success.set(null);
      await this.walletAdmin.deleteStaffUser(user.id);
      this.success.set(`${user.name} was deleted.`);
      await this.load();
    } catch (error) {
      this.error.set(this.apiErrorMessage(error, 'Failed to delete staff user.'));
    }
  }

  getStatus(user: StaffUserItem): 'active' | 'inactive' | 'suspended' {
    return user.status ?? (user.isActive ? 'active' : 'inactive');
  }

  private apiErrorMessage(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      const message = error.error?.message;
      if (typeof message === 'string' && message.trim()) {
        return message;
      }
    }

    return fallback;
  }
}
