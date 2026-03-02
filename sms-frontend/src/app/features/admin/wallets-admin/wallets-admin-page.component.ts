import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';

import { WalletAdminService } from '../../../core/wallet-admin.service';
import { AdminWalletItem } from '../../../core/wallet-admin.models';

@Component({
  selector: 'app-wallets-admin-page',
  imports: [CommonModule],
  templateUrl: './wallets-admin-page.component.html',
  styleUrl: './wallets-admin-page.component.scss'
})
export class WalletsAdminPageComponent {
  private readonly walletAdmin = inject(WalletAdminService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly wallets = signal<AdminWalletItem[]>([]);

  constructor() {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.wallets.set(await this.walletAdmin.getWallets());
    } catch {
      this.error.set('Failed to load wallets.');
    } finally {
      this.loading.set(false);
    }
  }

  async setStatus(wallet: AdminWalletItem, status: 'active' | 'suspended' | 'closed'): Promise<void> {
    try {
      await this.walletAdmin.updateWalletStatus(wallet.id, status);
      await this.load();
    } catch {
      this.error.set('Failed to update wallet status.');
    }
  }
}
