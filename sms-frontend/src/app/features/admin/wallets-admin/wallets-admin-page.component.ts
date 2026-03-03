import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { WalletAdminService } from '../../../core/wallet-admin.service';
import { AdminWalletItem, WalletCustomerItem } from '../../../core/wallet-admin.models';

@Component({
  selector: 'app-wallets-admin-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './wallets-admin-page.component.html',
  styleUrl: './wallets-admin-page.component.scss'
})
export class WalletsAdminPageComponent {
  private readonly walletAdmin = inject(WalletAdminService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly wallets = signal<AdminWalletItem[]>([]);
  readonly customers = signal<WalletCustomerItem[]>([]);
  readonly query = signal('');

  readonly createCustomerId = signal<number>(0);
  readonly createOpeningBalance = signal<number>(0);

  readonly editingWalletId = signal<string | null>(null);
  readonly editBalance = signal<number>(0);
  readonly editFrozen = signal(false);
  readonly editActive = signal(true);

  readonly filteredWallets = computed(() => {
    const normalized = this.query().trim().toLowerCase();
    if (!normalized) {
      return this.wallets();
    }

    return this.wallets().filter((wallet) =>
      wallet.id.toLowerCase().includes(normalized)
      || wallet.ownerName.toLowerCase().includes(normalized)
      || wallet.status.toLowerCase().includes(normalized)
      || (wallet.cardId ?? '').toLowerCase().includes(normalized)
      || (wallet.ownerPhone ?? '').toLowerCase().includes(normalized)
    );
  });

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [wallets, customers] = await Promise.all([
        this.walletAdmin.getWallets(),
        this.walletAdmin.getCustomers()
      ]);

      this.wallets.set(wallets);
      this.customers.set(customers);
      if (customers.length > 0 && this.createCustomerId() === 0) {
        this.createCustomerId.set(customers[0].id);
      }
    } catch {
      this.error.set('Failed to load wallets.');
    } finally {
      this.loading.set(false);
    }
  }

  async setStatus(wallet: AdminWalletItem, status: 'active' | 'suspended' | 'closed'): Promise<void> {
    try {
      this.error.set(null);
      this.success.set(null);
      await this.walletAdmin.updateWalletStatus(wallet.id, status);
      this.success.set(`Wallet #${wallet.id} moved to ${status}.`);
      await this.load();
    } catch {
      this.error.set('Failed to update wallet status.');
    }
  }

  beginEdit(wallet: AdminWalletItem): void {
    this.editingWalletId.set(wallet.id);
    this.editBalance.set(wallet.balance);
    this.editFrozen.set(wallet.isFrozen);
    this.editActive.set(wallet.isActive);
    this.error.set(null);
    this.success.set(null);
  }

  cancelEdit(): void {
    this.editingWalletId.set(null);
  }

  async saveEdit(wallet: AdminWalletItem): Promise<void> {
    try {
      this.error.set(null);
      this.success.set(null);
      await this.walletAdmin.updateWallet(wallet.id, {
        balance: Math.max(0, this.editBalance()),
        isFrozen: this.editFrozen(),
        isActive: this.editActive()
      });

      this.success.set(`Wallet #${wallet.id} updated.`);
      this.editingWalletId.set(null);
      await this.load();
    } catch {
      this.error.set('Failed to save wallet changes.');
    }
  }

  async createWallet(): Promise<void> {
    const customerId = this.createCustomerId();
    if (customerId <= 0) {
      this.error.set('Select a valid customer before creating a wallet.');
      return;
    }

    try {
      this.error.set(null);
      this.success.set(null);
      await this.walletAdmin.createWalletByCustomer(customerId, Math.max(0, this.createOpeningBalance()));
      this.success.set('Wallet has been created or already exists for the selected customer.');
      this.createOpeningBalance.set(0);
      await this.load();
    } catch {
      this.error.set('Failed to create wallet for the selected customer.');
    }
  }

  async deleteWallet(wallet: AdminWalletItem): Promise<void> {
    const approved = window.confirm(`Delete wallet #${wallet.id} for ${wallet.ownerName}? This cannot be undone.`);
    if (!approved) {
      return;
    }

    try {
      this.error.set(null);
      this.success.set(null);
      await this.walletAdmin.deleteWallet(wallet.id);
      this.success.set(`Wallet #${wallet.id} deleted.`);
      await this.load();
    } catch {
      this.error.set('Failed to delete wallet.');
    }
  }
}
