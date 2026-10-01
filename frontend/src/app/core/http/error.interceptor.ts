import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ToastService } from '../notifications/toast.service';
import { SKIP_ERROR_NOTIFICATION, getErrorMessage } from './api';

/**
 * Muestra un aviso con el mensaje de cualquier error HTTP.
 * Los 401 no se notifican aquí: los maneja authInterceptor (refresca la sesión o manda al login).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const toasts = inject(ToastService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status !== 401 && !req.context.get(SKIP_ERROR_NOTIFICATION)) {
        toasts.error(getErrorMessage(error));
      }
      return throwError(() => error);
    }),
  );
};
