import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEsCo from '@angular/common/locales/es-CO';
import { provideRouter, withComponentInputBinding, withInMemoryScrolling } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { errorInterceptor } from './core/http/error.interceptor';

registerLocaleData(localeEsCo);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding(), withInMemoryScrolling({ scrollPositionRestoration: 'top' })),
    // authInterceptor va primero: así errorInterceptor ve el resultado final (después del reintento con token nuevo).
    provideHttpClient(withInterceptors([errorInterceptor, authInterceptor])),
    { provide: LOCALE_ID, useValue: 'es-CO' },
    // Si hay cookie de refresh válida, la sesión se recupera antes de decidir la primera ruta.
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
