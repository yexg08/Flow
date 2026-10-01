import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Logo } from '../shared/logo';

@Component({
  selector: 'app-not-found',
  imports: [RouterLink, Logo],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="flex min-h-dvh flex-col items-center justify-center px-4 text-center">
      <app-logo [size]="40" />
      <p class="text-brand mt-10 text-7xl font-extrabold">404</p>
      <h1 class="mt-4 text-2xl font-bold">Esta página no existe</h1>
      <p class="mt-2 max-w-sm text-muted">Puede que el enlace esté mal escrito o que el negocio ya no esté en Flow.</p>
      <a routerLink="/" class="btn-primary mt-8">Volver al inicio</a>
    </main>
  `,
})
export class NotFound {}
