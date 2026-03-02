export type PortalRole = 'admin' | 'staff';

export interface AuthIdentity {
  staffUserId?: number;
  role: string;
  displayName: string;
  email?: string;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  identity: AuthIdentity;
}

export interface SessionUser {
  token: string;
  displayName: string;
  email: string;
  role: PortalRole;
  expiresAt: string;
  staffUserId?: number;
}

export interface AdminLoginRequest {
  usernameOrEmail: string;
  password: string;
  role: PortalRole;
}

export interface CreateAccountRequest {
  username: string;
  name: string;
  email: string;
  password: string;
  role: 'Admin' | 'Staff';
}

export interface ForgotPasswordRequest {
  usernameOrEmail: string;
  newPassword: string;
}
