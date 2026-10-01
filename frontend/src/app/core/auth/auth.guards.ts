import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/*
 * Los guards solo deciden qué pantalla ver: la seguridad real está en la API (denegar por defecto, datos filtrados
 * por negocio). Quien se salte un guard igual recibe 401/403/404 de la API.
 */

/** Panel del negocio: dueños y empleados. */
export const tenantGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  return auth.isTenantMember() ? true : router.parseUrl(auth.homeUrl());
};

/** Pantallas que solo usa el dueño (ajustes del negocio). Va detrás de tenantGuard. */
export const ownerGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isOwner() ? true : inject(Router).parseUrl('/app');
};

/** Panel de la plataforma: solo el superadmin. */
export const superAdminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  return auth.isSuperAdmin() ? true : router.parseUrl(auth.homeUrl());
};

/** Login y registro solo tienen sentido sin sesión. */
export const guestGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isAuthenticated() ? inject(Router).parseUrl(auth.homeUrl()) : true;
};
