import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE } from '../http/api';
import { AuthService } from './auth.service';

/** Endpoints que no llevan token ni se reintentan (el login fallido es un 401 normal, no una sesión vencida). */
const AUTH_ENDPOINTS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout', '/auth/slug-availability'];

const withToken = (req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

/**
 * Agrega el JWT a las peticiones de la API. Si una responde 401 (token vencido), refresca el token una vez y
 * reintenta; si el refresh falla, cierra la sesión.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  const isApiRequest = req.url.startsWith(API_BASE);
  const isAuthEndpoint = AUTH_ENDPOINTS.some((path) => req.url.startsWith(`${API_BASE}${path}`));
  if (!isApiRequest || isAuthEndpoint) return next(req);

  return next(withToken(req, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || !auth.isAuthenticated()) {
        return throwError(() => error);
      }

      return auth.refreshAccessToken().pipe(
        catchError((refreshError: unknown) => {
          auth.expireSession();
          return throwError(() => refreshError);
        }),
        switchMap((token) => next(withToken(req, token))),
      );
    }),
  );
};
