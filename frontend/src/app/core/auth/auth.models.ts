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
  /** Contraseña temporal: hasta cambiarla, la cuenta solo puede ir a la pantalla de cambio. */
  mustChangePassword: boolean;
  /** Si es un empleado: la persona del equipo a la que corresponde la cuenta. */
  staffMemberId: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
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
