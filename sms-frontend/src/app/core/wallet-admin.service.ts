import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from './auth.service';
import { AdminWalletItem, StaffUserItem, WalletCustomerItem } from './wallet-admin.models';

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
  phoneNumber?: string;
  phone?: string;
}

interface NfcCardDto {
  walletId: number;
  cardUid: string;
}

interface CreateWalletByCustomerRequest {
  customerId: number;
  openingBalance: number;
}

interface UpdateWalletRequest {
  balance?: number;
  isFrozen?: boolean;
  isActive?: boolean;
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
          customerId: customer?.id ?? 0,
          customerAccountId: wallet.customerAccountId,
          ownerName: customer?.name ?? `Account #${wallet.customerAccountId}`,
          ownerPhone: customer?.phoneNumber ?? customer?.phone ?? '',
          balance: account?.balance ?? 0,
          isFrozen: account?.isFrozen ?? false,
          isActive: wallet.isActive,
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

  async createWalletByCustomer(customerId: number, openingBalance: number): Promise<void> {
    const headers = this.authHeaders();
    const payload: CreateWalletByCustomerRequest = { customerId, openingBalance };
    await firstValueFrom(this.http.post(`${this.apiBase}/wallets/by-customer`, payload, { headers }));
  }

  async updateWallet(
    walletId: string,
    payload: {
      balance?: number;
      isFrozen?: boolean;
      isActive?: boolean;
    }
  ): Promise<void> {
    const headers = this.authHeaders();
    const request: UpdateWalletRequest = {};

    if (typeof payload.balance === 'number') {
      request.balance = payload.balance;
    }

    if (typeof payload.isFrozen === 'boolean') {
      request.isFrozen = payload.isFrozen;
    }

    if (typeof payload.isActive === 'boolean') {
      request.isActive = payload.isActive;
    }

    await firstValueFrom(this.http.put(`${this.apiBase}/wallets/${walletId}`, request, { headers }));
  }

  async deleteWallet(walletId: string): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.delete(`${this.apiBase}/wallets/${walletId}`, { headers }));
  }

  async getCustomers(): Promise<WalletCustomerItem[]> {
    const headers = this.authHeaders();
    const customers = await firstValueFrom(this.http.get<CustomerDto[]>(`${this.apiBase}/customers`, { headers }));
    return customers
      .map((item) => ({
        id: item.id,
        name: item.name,
        phone: item.phoneNumber ?? item.phone ?? ''
      }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }

  async createStaff(payload: {
    username: string;
    name: string;
    email: string;
    password: string;
    role: string;
  }): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.post(`${this.apiBase}/staff-users`, {
      username: payload.username.trim(),
      name: payload.name.trim(),
      email: payload.email.trim().toLowerCase(),
      password: payload.password,
      role: payload.role.trim().toLowerCase(),
      isActive: true
    }, { headers }));
  }

  async updateStaffStatus(id: number, status: 'active' | 'inactive' | 'suspended'): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.put(`${this.apiBase}/staff-users/${id}/status`, { status }, { headers }));
  }

  async updateStaffRole(id: number, role: 'admin' | 'staff' | 'manager'): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.put(`${this.apiBase}/staff-users/${id}/role`, { role }, { headers }));
  }

  async deleteStaffUser(id: number): Promise<void> {
    const headers = this.authHeaders();
    await firstValueFrom(this.http.delete(`${this.apiBase}/staff-users/${id}`, { headers }));
  }

  private authHeaders(): HttpHeaders {
    const token = this.auth.session()?.token;
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }
}
