export type Role = 'SuperAdmin' | 'Owner' | 'Staff';

export interface TenantSummary {
  id: string;
  name: string;
  slug: string;
}

export interface User {
  id: string;
  email: string;
  fullName: string;
  role: Role;
  /** Null solo para el superadmin. */
  tenant: TenantSummary | null;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: User;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  businessName: string;
  slug: string;
  fullName: string;
  email: string;
  password: string;
}

export interface SlugAvailability {
  slug: string;
  available: boolean;
  reason: string | null;
}
