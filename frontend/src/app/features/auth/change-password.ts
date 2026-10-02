import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { getErrorMessage, getFieldErrors } from '../../core/http/api';
import { ToastService } from '../../core/notifications/toast.service';
import { AuthLayout } from './auth-layout';

const PASSWORD_RULE = /^(?=.*[A-Za-zÀ-ÿ])(?=.*\d).{10,}$/;

/** Primer ingreso con contraseña temporal: hay que elegir una propia antes de usar el panel. */
@Component({
  selector: 'app-change-password',
  imports: [ReactiveFormsModule, AuthLayout],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-layout heading="Elige tu contraseña" [subheading]="'Hola, ' + firstName + '. Entraste con una contraseña temporal: cámbiala para empezar.'">
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="space-y-5">
        @if (error()) {
          <p class="rounded-xl border border-danger/40 bg-danger/10 px-3.5 py-2.5 text-sm text-danger" role="alert">{{ error() }}</p>
        }
        <div>
          <label for="current" class="field-label">Contraseña temporal</label>
          <input id="current" type="password" formControlName="currentPassword" autocomplete="current-password" class="field-input" [attr.aria-invalid]="invalid('currentPassword')" />
          @if (invalid('currentPassword')) {
            <p class="field-error">{{ serverErrors()['currentPassword'] ?? 'Escribe la contraseña temporal que te dieron.' }}</p>
          }
        </div>
        <div>
          <label for="new" class="field-label">Nueva contraseña</label>
          <input id="new" type="password" formControlName="newPassword" autocomplete="new-password" class="field-input" aria-describedby="new-hint" [attr.aria-invalid]="invalid('newPassword')" />
          @if (invalid('newPassword')) {
            <p id="new-hint" class="field-error">{{ serverErrors()['newPassword'] ?? 'Mínimo 10 caracteres, con letras y números.' }}</p>
          } @else {
            <p id="new-hint" class="mt-1.5 text-xs text-faint">Mínimo 10 caracteres, con letras y números.</p>
          }
        </div>
        <div>
          <label for="confirm" class="field-label">Repite la nueva contraseña</label>
          <input id="confirm" type="password" formControlName="confirm" autocomplete="new-password" class="field-input" [attr.aria-invalid]="mismatch()" />
          @if (mismatch()) {
            <p class="field-error">Las contraseñas no coinciden.</p>
          }
        </div>
        <button type="submit" class="btn-primary w-full py-3" [disabled]="loading()">{{ loading() ? 'Guardando…' : 'Guardar y entrar' }}</button>
        <button type="button" class="btn-ghost w-full" (click)="auth.logout()">Salir</button>
      </form>
    </app-auth-layout>
  `,
})
export class ChangePassword {
  protected readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);

  protected readonly firstName = this.auth.user()?.fullName.split(' ')[0] ?? '';
  protected readonly form = inject(NonNullableFormBuilder).group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]],
    confirm: ['', Validators.required],
  });
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string>>({});

  protected invalid(field: 'currentPassword' | 'newPassword'): boolean {
    const c = this.form.controls[field];
    return (c.invalid && c.touched) || !!this.serverErrors()[field];
  }

  protected mismatch(): boolean {
    const { newPassword, confirm } = this.form.getRawValue();
    return this.form.controls.confirm.touched && confirm !== newPassword;
  }

  protected submit(): void {
    if (this.form.invalid || this.mismatch()) {
      this.form.markAllAsTouched();
      return;
    }
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    this.auth.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.toasts.success('Listo, ya tienes tu propia contraseña.');
        void this.router.navigateByUrl(this.auth.homeUrl());
      },
      error: (e: unknown) => {
        const fields = getFieldErrors(e);
        this.serverErrors.set(fields);
        if (Object.keys(fields).length === 0) this.error.set(getErrorMessage(e));
        this.loading.set(false);
      },
    });
  }
}
