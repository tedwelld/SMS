import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import {
  AdminLoginRequest,
  AuthResponse,
  CreateAccountRequest,
  ForgotPasswordRequest,
  PortalRole,
  SessionUser
} from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = 'http://localhost:5032/api/auth';
  private readonly storageKey = 'sms_auth_session';

  readonly session = signal<SessionUser | null>(this.readStoredSession());
  readonly isAuthenticated = computed(() => this.session() !== null);

  get role(): PortalRole {
    return this.session()?.role ?? 'staff';
  }

  async login(request: AdminLoginRequest): Promise<SessionUser> {
    const response = await firstValueFrom(
      this.http.post<AuthResponse>(`${this.apiBaseUrl}/admin/login`, {
        usernameOrEmail: request.usernameOrEmail,
        password: request.password
      })
    );

    const roleFromApi = response.identity.role?.toLowerCase() === 'staff' ? 'staff' : 'admin';
    const role = request.role === 'staff' ? 'staff' : roleFromApi;

    const nextSession: SessionUser = {
      token: response.token,
      displayName: response.identity.displayName,
      email: response.identity.email ?? '',
      role,
      staffUserId: response.identity.staffUserId,
      expiresAt: response.expiresAt
    };

    this.persistSession(nextSession);
    return nextSession;
  }

  async createAccount(request: CreateAccountRequest): Promise<void> {
    await firstValueFrom(this.http.post(`${this.apiBaseUrl}/create-account`, request));
  }

  async forgotPassword(request: ForgotPasswordRequest): Promise<void> {
    await firstValueFrom(this.http.post(`${this.apiBaseUrl}/forgot-password`, request));
  }

  logout(): void {
    this.session.set(null);
    localStorage.removeItem(this.storageKey);
  }

  authHeader(): string {
    return this.session()?.token ? `Bearer ${this.session()!.token}` : '';
  }

  apiErrorMessage(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error?.message === 'string') {
        return error.error.message;
      }

      if (typeof error.message === 'string') {
        return error.message;
      }
    }

    return 'Request failed. Check that the API is running on http://localhost:5032.';
  }

  private persistSession(session: SessionUser): void {
    this.session.set(session);
    localStorage.setItem(this.storageKey, JSON.stringify(session));
  }

  private readStoredSession(): SessionUser | null {
    try {
      const raw = localStorage.getItem(this.storageKey);
      if (!raw) {
        return null;
      }

      const parsed = JSON.parse(raw) as SessionUser;
      if (!parsed.token || !parsed.expiresAt) {
        return null;
      }

      if (new Date(parsed.expiresAt) <= new Date()) {
        localStorage.removeItem(this.storageKey);
        return null;
      }

      return parsed;
    } catch {
      localStorage.removeItem(this.storageKey);
      return null;
    }
  }
}
