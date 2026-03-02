import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from './auth.service';
import { AdminWalletItem, StaffUserItem } from './wallet-admin.models';

interface WalletDto {
  id: number;
  customerAccountId: number;
  isActive: boolean;
  dateCreated: string;
}

interface CustomerAccountDto {
  id: number;
  customerId: number;
  accountNumber: string;
  balance: number;
  isFrozen: boolean;
}

interface CustomerDto {
  id: number;
  name: string;
}

interface NfcCardDto {
  walletId: number;
  cardUid: string;
}

@Injectable({ providedIn: 'root' })
export class WalletAdminService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly apiBase = 'http://localhost:5032/api';

  async getWallets(): Promise<AdminWalletItem[]> {
    const headers = this.authHeaders();
    const [wallets, accounts, customers, cards] = await Promise.all([
      firstValueFrom(this.http.get<WalletDto[]>(`${this.apiBase}/wallets`, { headers })),
      firstValueFrom(this.http.get<CustomerAccountDto[]>(`${this.apiBase}/customer-accounts`, { headers })),
      firstValueFrom(this.http.get<CustomerDto[]>(`${this.apiBase}/customers`, { headers })),
      firstValueFrom(this.http.get<NfcCardDto[]>(`${this.apiBase}/nfc-cards`, { headers }))
    ]);

    return wallets
      .map((wallet) => {
        const account = accounts.find((item) => item.id === wallet.customerAccountId);
        const customer = account ? customers.find((item) => item.id === account.customerId) : undefined;
        const card = cards.find((item) => item.walletId === wallet.id);

        const status: AdminWalletItem['status'] = !wallet.isActive
          ? 'closed'
          : account?.isFrozen
            ? 'suspended'
            : 'active';

        return {
          id: String(wallet.id),
          ownerName: customer?.name ?? `Account #${wallet.customerAccountId}`,
          balance: account?.balance ?? 0,
          status,
          cardId: card?.cardUid,
          createdAt: wallet.dateCreated
        };
      })
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }

  async getStaffUsers(): Promise<StaffUserItem[]> {
    const headers = this.authHeaders();
    return firstValueFrom(this.http.get<StaffUserItem[]>(`${this.apiBase}/staff-users`, { headers }));
  }

  async updateWalletStatus(walletId: string, status: 'active' | 'suspended' | 'closed'): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.put(`${this.apiBase}/wallets/${walletId}/status`, { status }, { headers }));
  }

  async createStaff(payload: {
    username: string;
    name: string;
    email: string;
    password: string;
    role: string;
  }): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.post(`${this.apiBase}/staff-users`, payload, { headers }));
  }

  async updateStaffStatus(id: number, status: 'active' | 'inactive' | 'suspended'): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.put(`${this.apiBase}/staff-users/${id}/status`, { status }, { headers }));
  }

  async updateStaffRole(id: number, role: 'admin' | 'staff' | 'manager'): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.put(`${this.apiBase}/staff-users/${id}/role`, { role }, { headers }));
  }

  private authHeaders(): HttpHeaders {
    const token = this.auth.session()?.token;
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }
}
