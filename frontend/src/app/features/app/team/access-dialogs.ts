import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroCheck, heroClipboardDocument, heroExclamationTriangle, heroKey } from '@ng-icons/heroicons/outline';
import { API_BASE, getErrorMessage, getFieldErrors, silentErrors } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { formatDate } from '../../../shared/format';

export interface TemporaryPassword {
  email: string;
  temporaryPassword: string;
  expiresAt: string;
}

export interface StaffAccount {
  staffMemberId: string;
  email: string;
  lastLoginAt: string | null;
  pendingFirstLogin: boolean;
  temporaryPasswordExpiresAt: string | null;
}

/** Pide el correo de la persona y crea su cuenta. Cierra con la contraseña temporal. */
@Component({
  selector: 'app-grant-access-dialog',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form [formGroup]="form" (ngSubmit)="save()" novalidate class="card w-[min(28rem,calc(100vw-2rem))] p-6 shadow-2xl" aria-labelledby="grant-title">
      <h2 id="grant-title" class="text-lg font-bold">Dar acceso al panel a {{ data.name }}</h2>
      <p class="mt-1 text-sm text-muted">
        Podrá ver la agenda, agendar citas y marcar asistencia. No podrá cambiar servicios, equipo ni ajustes.
      </p>
      <div class="mt-5">
        <label for="grant-email" class="field-label">Su correo</label>
        <input id="grant-email" type="email" formControlName="email" autocomplete="off" class="field-input" [attr.aria-invalid]="!!error()" />
        @if (error()) {
          <p class="field-error" role="alert">{{ error() }}</p>
        }
      </div>
      <div class="mt-6 flex justify-end gap-2">
        <button type="button" class="btn-secondary" (click)="ref.close()">Cancelar</button>
        <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Creando…' : 'Crear acceso' }}</button>
      </div>
    </form>
  `,
})
export class GrantAccessDialog {
  private readonly http = inject(HttpClient);
  protected readonly data = inject<{ staffId: string; name: string }>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<TemporaryPassword>>(DialogRef);
  protected readonly form = inject(NonNullableFormBuilder).group({ email: ['', [Validators.required, Validators.email]] });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected save(): void {
    if (this.form.invalid) {
      this.error.set('Escribe un correo válido.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.http
      .post<TemporaryPassword>(`${API_BASE}/staff/${this.data.staffId}/account`, this.form.getRawValue(), { context: silentErrors() })
      .subscribe({
        next: (result) => this.ref.close(result),
        error: (e: unknown) => {
          this.error.set(getFieldErrors(e)['email'] ?? getErrorMessage(e));
          this.saving.set(false);
        },
      });
  }
}

/** Muestra la contraseña temporal UNA sola vez, con un mensaje listo para enviar por WhatsApp. */
@Component({
  selector: 'app-temporary-password-dialog',
  imports: [NgIcon],
  providers: [provideIcons({ heroCheck, heroClipboardDocument, heroExclamationTriangle, heroKey })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="card w-[min(30rem,calc(100vw-2rem))] p-6 shadow-2xl" role="dialog" aria-labelledby="temp-title">
      <div class="flex size-11 items-center justify-center rounded-xl bg-violet-500/15 text-accent"><ng-icon name="heroKey" size="22" /></div>
      <h2 id="temp-title" class="mt-4 text-lg font-bold">Acceso listo para {{ data.name }}</h2>
      <p class="mt-1 text-sm text-muted">Entra con <strong class="text-ink">{{ data.result.email }}</strong> y esta contraseña temporal:</p>

      <p class="mt-4 rounded-xl border border-line bg-surface-2 px-4 py-3 text-center font-mono text-lg font-bold tracking-wider select-all">
        {{ data.result.temporaryPassword }}
      </p>
      <p class="mt-3 flex items-start gap-2 text-xs text-muted">
        <ng-icon name="heroExclamationTriangle" size="15" class="mt-px shrink-0 text-accent" />
        Solo la verás ahora. Vence el {{ expires }} y al entrar le pedirá elegir su propia contraseña.
      </p>

      <div class="mt-6 grid gap-2 sm:grid-cols-2">
        <button type="button" class="btn-secondary" (click)="copy(data.result.temporaryPassword, 'password')">
          <ng-icon [name]="copied() === 'password' ? 'heroCheck' : 'heroClipboardDocument'" size="17" />
          {{ copied() === 'password' ? 'Copiada' : 'Copiar contraseña' }}
        </button>
        <button type="button" class="btn-primary" (click)="copy(message(), 'message')">
          <ng-icon [name]="copied() === 'message' ? 'heroCheck' : 'heroClipboardDocument'" size="17" />
          {{ copied() === 'message' ? 'Mensaje copiado' : 'Copiar mensaje para WhatsApp' }}
        </button>
      </div>
      <button type="button" class="btn-ghost mt-2 w-full" (click)="ref.close()">Listo</button>
    </div>
  `,
})
export class TemporaryPasswordDialog {
  private readonly toasts = inject(ToastService);
  protected readonly data = inject<{ name: string; business: string; result: TemporaryPassword }>(DIALOG_DATA);
  protected readonly ref = inject(DialogRef);
  protected readonly copied = signal<'password' | 'message' | null>(null);
  protected readonly expires = formatDate(this.data.result.expiresAt);

  protected readonly message = computed(
    () =>
      `Hola ${this.data.name.split(' ')[0]}, ya tienes acceso a ${this.data.business} en Flow.\n\n` +
      `Entra en: ${location.origin}/login\nCorreo: ${this.data.result.email}\nContraseña temporal: ${this.data.result.temporaryPassword}\n\n` +
      `Vence el ${this.expires}. Al entrar te pedirá elegir tu propia contraseña.`,
  );

  protected async copy(text: string, what: 'password' | 'message'): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.copied.set(what);
      setTimeout(() => this.copied.set(null), 2000);
    } catch {
      this.toasts.error('No se pudo copiar. Selecciona el texto y cópialo a mano.');
    }
  }
}
