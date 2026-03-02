import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

import { StaffUserItem } from '../../../core/wallet-admin.models';
import { WalletAdminService } from '../../../core/wallet-admin.service';

@Component({
  selector: 'app-staff-admin-page',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './staff-admin-page.component.html',
  styleUrl: './staff-admin-page.component.scss'
})
export class StaffAdminPageComponent {
  private readonly walletAdmin = inject(WalletAdminService);
  private readonly fb = inject(FormBuilder);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly staffUsers = signal<StaffUserItem[]>([]);

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
    try {
      this.staffUsers.set(await this.walletAdmin.getStaffUsers());
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
      await this.load();
    } catch {
      this.error.set('Failed to create staff user.');
    } finally {
      this.submitting.set(false);
    }
  }

  async toggleStatus(user: StaffUserItem): Promise<void> {
    const next = user.isActive ? 'inactive' : 'active';
    try {
      await this.walletAdmin.updateStaffStatus(user.id, next);
      await this.load();
    } catch {
      this.error.set('Failed to update staff status.');
    }
  }
}
