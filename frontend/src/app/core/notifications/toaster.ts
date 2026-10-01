import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroCheckCircle, heroExclamationTriangle, heroInformationCircle, heroXMark } from '@ng-icons/heroicons/outline';
import { ToastService } from './toast.service';

@Component({
  selector: 'app-toaster',
  imports: [NgIcon],
  providers: [provideIcons({ heroCheckCircle, heroExclamationTriangle, heroInformationCircle, heroXMark })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pointer-events-none fixed inset-x-0 bottom-4 z-[1100] flex flex-col items-center gap-2 px-4" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div
          class="card pointer-events-auto flex w-full max-w-md items-start gap-3 px-4 py-3 shadow-2xl shadow-black/20"
          [attr.role]="toast.kind === 'error' ? 'alert' : 'status'"
        >
          @switch (toast.kind) {
            @case ('success') {
              <ng-icon name="heroCheckCircle" class="mt-0.5 shrink-0 text-success" size="20" />
            }
            @case ('error') {
              <ng-icon name="heroExclamationTriangle" class="mt-0.5 shrink-0 text-danger" size="20" />
            }
            @default {
              <ng-icon name="heroInformationCircle" class="mt-0.5 shrink-0 text-accent" size="20" />
            }
          }
          <p class="flex-1 text-sm">{{ toast.message }}</p>
          <button type="button" class="text-faint hover:text-ink" aria-label="Cerrar aviso" (click)="toasts.dismiss(toast.id)">
            <ng-icon name="heroXMark" size="18" />
          </button>
        </div>
      }
    </div>
  `,
})
export class Toaster {
  protected readonly toasts = inject(ToastService);
}
