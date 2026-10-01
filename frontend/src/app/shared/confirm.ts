import { Dialog, DialogConfig, DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmText?: string;
  danger?: boolean;
}

/** Fondo oscuro y foco inicial para todos los diálogos de Flow. */
export const DIALOG_DEFAULTS: Pick<DialogConfig, 'backdropClass' | 'autoFocus'> = {
  backdropClass: ['bg-black/60', 'backdrop-blur-sm'],
  autoFocus: 'first-tabbable',
};

@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="card w-[min(28rem,calc(100vw-2rem))] p-6 shadow-2xl"
      role="alertdialog"
      aria-labelledby="confirm-title"
      aria-describedby="confirm-message"
    >
      <h2 id="confirm-title" class="text-lg font-bold">{{ data.title }}</h2>
      <p id="confirm-message" class="mt-2 text-sm text-muted">{{ data.message }}</p>
      <div class="mt-6 flex justify-end gap-2">
        <button type="button" class="btn-secondary" (click)="ref.close(false)">Cancelar</button>
        <button type="button" [class]="data.danger ? 'btn-danger' : 'btn-primary'" (click)="ref.close(true)">
          {{ data.confirmText ?? 'Confirmar' }}
        </button>
      </div>
    </div>
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmOptions>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
}

/** Pregunta antes de una acción delicada. Emite true solo si el usuario confirma. */
@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(Dialog);

  ask(options: ConfirmOptions): Observable<boolean> {
    return this.dialog
      .open<boolean>(ConfirmDialog, { data: options, ...DIALOG_DEFAULTS })
      .closed.pipe(map((result) => result === true));
  }
}
