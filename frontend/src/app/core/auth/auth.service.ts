import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay } from 'rxjs';
import { API_BASE, silentErrors } from '../http/api';
import { ToastService } from '../notifications/toast.service';
import { AuthResponse, ChangePasswordRequest, LoginRequest, RegisterRequest, SlugAvailability, TenantSummary, User } from './auth.models';

/**
 * Estado de la sesión.
 * - El access token (JWT, 15 min) vive solo en memoria: no se guarda en localStorage.
 * - El refresh token lo maneja el navegador en una cookie httpOnly; al recargar se recupera la sesión con él.
 * Los roles aquí solo deciden qué pantallas mostrar: los permisos reales los aplica la API.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  private readonly currentUser = signal<User | null>(null);
  private readonly token = signal<string | null>(null);
  private refreshInFlight$: Observable<string> | null = null;

  readonly user = this.currentUser.asReadonly();
  readonly accessToken = this.token.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly isSuperAdmin = computed(() => this.currentUser()?.role === 'SuperAdmin');
  readonly isOwner = computed(() => this.currentUser()?.role === 'Owner');
  readonly isTenantMember = computed(() => this.currentUser()?.tenant != null);
  readonly tenant = computed(() => this.currentUser()?.tenant ?? null);
  readonly mustChangePassword = computed(() => this.currentUser()?.mustChangePassword ?? false);

  /**
   * A dónde va cada cuenta al entrar: primero a cambiar la contraseña temporal; luego el superadmin a la plataforma
   * y dueños y empleados a su negocio.
   */
  homeUrl(): string {
    if (!this.isAuthenticated()) return '/login';
    if (this.mustChangePassword()) return '/cambiar-contrasena';
    return this.isSuperAdmin() ? '/admin' : '/app';
  }

  /** La API cierra las demás sesiones y devuelve tokens nuevos para esta. */
  changePassword(request: ChangePasswordRequest): Observable<User> {
    return this.http
      .post<AuthResponse>(`${API_BASE}/auth/change-password`, request, { context: silentErrors() })
      .pipe(map((response) => this.setSession(response)));
  }

  login(credentials: LoginRequest): Observable<User> {
    return this.http
      .post<AuthResponse>(`${API_BASE}/auth/login`, credentials, { context: silentErrors() })
      .pipe(map((response) => this.setSession(response)));
  }

  register(request: RegisterRequest): Observable<User> {
    return this.http
      .post<AuthResponse>(`${API_BASE}/auth/register`, request, { context: silentErrors() })
      .pipe(map((response) => this.setSession(response)));
  }

  checkSlug(slug: string): Observable<SlugAvailability> {
    return this.http.get<SlugAvailability>(`${API_BASE}/auth/slug-availability`, {
      params: new HttpParams().set('slug', slug),
      context: silentErrors(),
    });
  }

  /** Mantiene el nombre y el enlace del negocio al día después de editarlos en Ajustes. */
  updateTenant(tenant: TenantSummary): void {
    this.currentUser.update((user) => (user ? { ...user, tenant } : user));
  }

  /** Se llama al iniciar la app: si hay cookie de refresh válida, la sesión continúa sin pedir login. */
  restoreSession(): Promise<void> {
    return firstValueFrom(
      this.refreshAccessToken().pipe(
        map(() => undefined),
        catchError(() => of(undefined)),
      ),
    );
  }

  /** Pide un access token nuevo. Si varias peticiones lo necesitan a la vez, comparten una sola llamada. */
  refreshAccessToken(): Observable<string> {
    this.refreshInFlight$ ??= this.http
      .post<AuthResponse>(`${API_BASE}/auth/refresh`, null, { context: silentErrors() })
      .pipe(
        map((response) => {
          this.setSession(response);
          return response.accessToken;
        }),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay({ bufferSize: 1, refCount: false }),
      );
    return this.refreshInFlight$;
  }

  logout(): void {
    this.http
      .post<void>(`${API_BASE}/auth/logout`, null, { context: silentErrors() })
      .pipe(catchError(() => of(undefined)))
      .subscribe(() => {
        this.clearSession();
        void this.router.navigate(['/login']);
      });
  }

  /** El refresh falló (sesión vencida, cuenta desactivada o negocio suspendido). */
  expireSession(): void {
    if (!this.isAuthenticated()) return;
    const returnUrl = this.router.url;
    this.clearSession();
    this.toasts.info('Tu sesión terminó. Inicia sesión de nuevo.');
    void this.router.navigate(['/login'], { queryParams: { returnUrl } });
  }

  private setSession(response: AuthResponse): User {
    this.token.set(response.accessToken);
    this.currentUser.set(response.user);
    return response.user;
  }

  private clearSession(): void {
    this.token.set(null);
    this.currentUser.set(null);
  }
}
