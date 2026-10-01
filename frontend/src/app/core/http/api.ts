import { HttpContext, HttpContextToken, HttpErrorResponse } from '@angular/common/http';

/** Base de la API. En desarrollo el proxy de Angular la redirige a http://localhost:5090. */
export const API_BASE = '/api';

/** Marca una petición para que el interceptor de errores no muestre aviso (el componente lo maneja). */
export const SKIP_ERROR_NOTIFICATION = new HttpContextToken<boolean>(() => false);

export const silentErrors = (): HttpContext => new HttpContext().set(SKIP_ERROR_NOTIFICATION, true);

/** Formato de error estándar que devuelve la API (RFC 9457). */
export interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && ('title' in value || 'detail' in value);
}

/** Errores de validación por campo (camelCase, como los envía la API). */
export function getFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof HttpErrorResponse) || !isProblemDetails(error.error) || !error.error.errors) return {};
  return Object.fromEntries(Object.entries(error.error.errors).map(([field, messages]) => [field, messages[0] ?? '']));
}

/** Convierte cualquier error HTTP en un mensaje en español apto para mostrar al usuario. */
export function getErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'Ocurrió un error inesperado.';
  if (error.status === 0) return 'No se pudo conectar con el servidor. Verifica que la API esté corriendo.';
  if (error.status === 403) return 'No tienes permiso para hacer esto.';
  if (error.status === 429) return 'Demasiados intentos. Espera un minuto e inténtalo de nuevo.';

  const body: unknown = error.error;
  if (isProblemDetails(body)) {
    const firstValidationError = body.errors ? Object.values(body.errors).flat()[0] : undefined;
    return firstValidationError ?? body.detail ?? body.title ?? 'Ocurrió un error inesperado.';
  }

  return error.status >= 500 ? 'Ocurrió un error en el servidor. Intenta de nuevo más tarde.' : 'Ocurrió un error inesperado.';
}
