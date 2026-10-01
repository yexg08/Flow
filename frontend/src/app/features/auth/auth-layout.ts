import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Logo } from '../../shared/logo';
import { ThemeToggle } from '../../shared/theme-toggle';

/** Marco común de login y registro: logo, tarjeta centrada y resplandor de marca. */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterLink, Logo, ThemeToggle],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative flex min-h-dvh flex-col overflow-hidden">
      <div aria-hidden="true" class="pointer-events-none absolute top-[-14rem] left-1/2 -z-10 h-[30rem] w-[44rem] -translate-x-1/2 rounded-full bg-violet-600/20 blur-3xl"></div>
      <header class="mx-auto flex w-full max-w-6xl items-center justify-between px-4 py-5 sm:px-6">
        <a routerLink="/" aria-label="Flow, inicio"><app-logo /></a>
        <app-theme-toggle />
      </header>
      <main class="flex flex-1 items-start justify-center px-4 pt-6 pb-16 sm:items-center sm:pt-0">
        <div class="card w-full p-6 shadow-2xl shadow-violet-950/20 sm:p-8" [class]="wide() ? 'max-w-xl' : 'max-w-md'">
          <h1 class="text-2xl font-extrabold tracking-tight">{{ heading() }}</h1>
          <p class="mt-1.5 text-sm text-muted">{{ subheading() }}</p>
          <div class="mt-7"><ng-content /></div>
        </div>
      </main>
    </div>
  `,
})
export class AuthLayout {
  readonly heading = input.required<string>();
  readonly subheading = input.required<string>();
  readonly wide = input(false);
}
