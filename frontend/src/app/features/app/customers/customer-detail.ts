import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroArrowLeft, heroChatBubbleLeftRight } from '@ng-icons/heroicons/outline';
import { API_BASE, getErrorMessage, silentErrors } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { formatDate, formatPrice, whatsAppNumber } from '../../../shared/format';
import { formatTime } from '../../../shared/schedule';
import { AgendaAppointment, STATUS_BADGE, STATUS_LABEL } from '../agenda/agenda.models';

interface CustomerDetail {
  id: string;
  name: string;
  phone: string;
  email: string | null;
  notes: string | null;
  createdAt: string;
  consentedOnline: boolean;
  visits: number;
  noShows: number;
  appointments: AgendaAppointment[];
}

@Component({
  selector: 'app-customer-detail',
  imports: [ReactiveFormsModule, RouterLink, NgIcon],
  providers: [provideIcons({ heroArrowLeft, heroChatBubbleLeftRight })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-4xl">
      <a routerLink="/app/clientes" class="inline-flex items-center gap-1.5 text-sm font-medium text-muted hover:text-ink">
        <ng-icon name="heroArrowLeft" size="16" /> Clientes
      </a>

      @if (customer.isLoading() && !customer.hasValue()) {
        <div class="card mt-6 h-72 animate-pulse bg-surface-2"></div>
      } @else if (customer.error()) {
        <div class="card mt-6 p-8 text-center">
          <p class="font-semibold">No se encontró este cliente.</p>
        </div>
      } @else if (customer.hasValue()) {
        @let c = customer.value();
        <div class="mt-4 flex flex-wrap items-center justify-between gap-4">
          <div class="flex items-center gap-4">
            <span class="bg-brand flex size-14 items-center justify-center rounded-full text-2xl font-bold text-white" aria-hidden="true">
              {{ c.name.charAt(0).toUpperCase() }}
            </span>
            <div>
              <h1 class="text-2xl font-extrabold tracking-tight">{{ c.name }}</h1>
              <p class="text-sm text-muted">Cliente desde {{ date(c.createdAt) }}</p>
            </div>
          </div>
          <a [href]="'https://wa.me/' + whatsApp(c.phone)" target="_blank" rel="noopener noreferrer" class="btn-secondary">
            <ng-icon name="heroChatBubbleLeftRight" size="18" /> {{ c.phone }}
            <span class="sr-only">(WhatsApp, se abre en otra pestaña)</span>
          </a>
        </div>

        <div class="mt-6 grid grid-cols-3 gap-3">
          <div class="card p-4"><p class="text-sm text-muted">Visitas</p><p class="mt-1 text-2xl font-extrabold tabular-nums">{{ c.visits }}</p></div>
          <div class="card p-4"><p class="text-sm text-muted">Inasistencias</p><p class="mt-1 text-2xl font-extrabold tabular-nums" [class.text-danger]="c.noShows > 0">{{ c.noShows }}</p></div>
          <div class="card p-4"><p class="text-sm text-muted">Citas en total</p><p class="mt-1 text-2xl font-extrabold tabular-nums">{{ c.appointments.length }}</p></div>
        </div>

        <div class="mt-6 grid gap-6 lg:grid-cols-[1fr_20rem]">
          <section aria-labelledby="history-title">
            <h2 id="history-title" class="text-lg font-bold">Historial</h2>
            <ul class="card mt-3 divide-y divide-line">
              @for (a of c.appointments; track a.id) {
                <li class="flex items-center gap-4 px-4 py-3">
                  <span class="h-10 w-1 shrink-0 rounded-full" [style.background-color]="a.staffColor" aria-hidden="true"></span>
                  <div class="min-w-0 flex-1">
                    <p class="font-semibold">{{ when(a.startsAt) }}</p>
                    <p class="truncate text-sm text-muted">{{ a.serviceName }} · con {{ a.staffName }} · {{ price(a.price) }}</p>
                  </div>
                  <span class="shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold" [class]="badge[a.status]">{{ label[a.status] }}</span>
                </li>
              } @empty {
                <li class="p-6 text-center text-sm text-muted">Sin citas.</li>
              }
            </ul>
          </section>

          <form [formGroup]="form" (ngSubmit)="save()" novalidate class="card h-fit space-y-4 p-5" aria-labelledby="data-title">
            <h2 id="data-title" class="font-bold">Datos</h2>
            <div>
              <label for="c-name" class="field-label">Nombre</label>
              <input id="c-name" formControlName="name" class="field-input" />
            </div>
            <div>
              <label for="c-email" class="field-label">Correo</label>
              <input id="c-email" type="email" formControlName="email" class="field-input" />
            </div>
            <div>
              <label for="c-notes" class="field-label">Notas internas</label>
              <textarea id="c-notes" formControlName="notes" rows="4" class="field-input resize-none" placeholder="Preferencias, alergias... El cliente no las ve."></textarea>
            </div>
            <p class="text-xs text-faint">
              {{ c.consentedOnline ? 'Autorizó el uso de sus datos al reservar en línea.' : 'Registrado desde el panel: la autorización de datos la gestiona tu negocio.' }}
            </p>
            <button type="submit" class="btn-primary w-full" [disabled]="saving() || form.pristine || form.invalid">
              {{ saving() ? 'Guardando…' : 'Guardar' }}
            </button>
          </form>
        </div>
      }
    </div>
  `,
})
export class CustomerDetailPage {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);

  /** Viene de la ruta clientes/:id. */
  readonly id = input.required<string>();

  protected readonly customer = httpResource<CustomerDetail>(() => `${API_BASE}/customers/${this.id()}`);
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.email, Validators.maxLength(256)]],
    notes: ['', [Validators.maxLength(500)]],
  });
  protected readonly saving = signal(false);

  protected readonly label = STATUS_LABEL;
  protected readonly badge = STATUS_BADGE;
  protected readonly date = formatDate;
  protected readonly whatsApp = whatsAppNumber;
  protected readonly price = (value: number) => formatPrice(value);

  constructor() {
    effect(() => {
      if (!this.customer.hasValue()) return;
      const c = this.customer.value();
      this.form.reset({ name: c.name, email: c.email ?? '', notes: c.notes ?? '' });
    });
  }

  protected when(localIso: string): string {
    return `${formatDate(localIso)}, ${formatTime(localIso.slice(11, 16))}`;
  }

  protected save(): void {
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.http
      .put<CustomerDetail>(
        `${API_BASE}/customers/${this.id()}`,
        { name: v.name.trim(), email: v.email.trim() || null, notes: v.notes.trim() || null },
        { context: silentErrors() },
      )
      .subscribe({
        next: (updated) => {
          this.customer.set(updated);
          this.saving.set(false);
          this.toasts.success('Datos guardados.');
        },
        error: (e: unknown) => {
          this.saving.set(false);
          this.toasts.error(getErrorMessage(e));
        },
      });
  }
}
