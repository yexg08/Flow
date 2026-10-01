import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroEye, heroEyeSlash } from '@ng-icons/heroicons/outline';
import { AuthService } from '../../core/auth/auth.service';
import { getErrorMessage } from '../../core/http/api';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, NgIcon, AuthLayout],
  providers: [provideIcons({ heroEye, heroEyeSlash })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-layout heading="Inicia sesión" subheading="Entra al panel de tu negocio.">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="space-y-5">
        @if (error()) {
          <p class="rounded-xl border border-danger/40 bg-danger/10 px-3.5 py-2.5 text-sm text-danger" role="alert">{{ error() }}</p>
        }
        <div>
          <label for="email" class="field-label">Correo</label>
          <input id="email" type="email" formControlName="email" autocomplete="email" class="field-input" [attr.aria-invalid]="invalid('email')" />
          @if (invalid('email')) {
            <p class="field-error">Escribe tu correo.</p>
          }
        </div>
        <div>
          <label for="password" class="field-label">Contraseña</label>
          <div class="relative">
            <input
              id="password"
              [type]="showPassword() ? 'text' : 'password'"
              formControlName="password"
              autocomplete="current-password"
              class="field-input pr-11"
              [attr.aria-invalid]="invalid('password')"
            />
            <button
              type="button"
              class="absolute inset-y-0 right-0 flex w-11 items-center justify-center text-faint hover:text-ink"
              (click)="showPassword.set(!showPassword())"
              [attr.aria-label]="showPassword() ? 'Ocultar contraseña' : 'Mostrar contraseña'"
            >
              <ng-icon [name]="showPassword() ? 'heroEyeSlash' : 'heroEye'" size="18" />
            </button>
          </div>
          @if (invalid('password')) {
            <p class="field-error">Escribe tu contraseña.</p>
          }
        </div>
        <button type="submit" class="btn-primary w-full py-3" [disabled]="loading()">
          {{ loading() ? 'Entrando…' : 'Iniciar sesión' }}
        </button>
      </form>
      <p class="mt-6 text-center text-sm text-muted">
        ¿Aún no tienes cuenta?
        <a routerLink="/registro" class="font-semibold text-accent hover:underline">Crea tu negocio</a>
      </p>
    </app-auth-layout>
  `,
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  /** A dónde volver después de iniciar sesión (lo pone el guard al redirigir). */
  readonly returnUrl = input<string>();

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected invalid(control: 'email' | 'password'): boolean {
    const c = this.form.controls[control];
    return c.invalid && c.touched;
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.auth.login(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigateByUrl(this.safeReturnUrl() ?? this.auth.homeUrl()),
      error: (e: unknown) => {
        this.error.set(getErrorMessage(e));
        this.loading.set(false);
      },
    });
  }

  /** Solo rutas internas: evita que un enlace manipulado mande al usuario a otro sitio después de entrar. */
  private safeReturnUrl(): string | null {
    const url = this.returnUrl();
    if (!url || !url.startsWith('/') || url.startsWith('//')) return null;
    const home = this.auth.homeUrl();
    return url.startsWith(home) ? url : null;
  }
}
