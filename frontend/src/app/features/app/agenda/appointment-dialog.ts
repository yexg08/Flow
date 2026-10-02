import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpClient, httpResource } from '@angular/common/http';
import { AuthService } from '../../../core/auth/auth.service';
import { confirmationMessage, reminderMessage, whatsAppLink } from '../../../shared/messages';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { Observable } from 'rxjs';
import {
  heroArrowPath,
  heroArrowsRightLeft,
  heroBellAlert,
  heroChatBubbleLeftRight,
  heroCheckCircle,
  heroClock,
  heroNoSymbol,
  heroUser,
  heroXCircle,
  heroXMark,
} from '@ng-icons/heroicons/outline';
import { API_BASE, getErrorMessage, silentErrors } from '../../../core/http/api';
import { formatDuration, formatPrice, whatsAppNumber } from '../../../shared/format';
import { formatTime, parseLocal } from '../../../shared/schedule';
import { StaffMember } from '../business.models';
import { AgendaAppointment, AppointmentStatus, STATUS_BADGE, STATUS_LABEL } from './agenda.models';

export interface AppointmentDialogData {
  appointment: AgendaAppointment;
  staff: StaffMember[];
}

/** Detalle de una cita en la agenda: cambiar su estado o moverla. La agenda se recarga al cerrarlo. */
@Component({
  selector: 'app-appointment-dialog',
  imports: [ReactiveFormsModule, RouterLink, NgIcon],
  providers: [
    provideIcons({ heroArrowPath, heroArrowsRightLeft, heroBellAlert, heroChatBubbleLeftRight, heroCheckCircle, heroClock, heroNoSymbol, heroUser, heroXCircle, heroXMark }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let a = appointment();
    <div class="card flex max-h-[calc(100dvh-2rem)] w-[min(30rem,calc(100vw-2rem))] flex-col shadow-2xl" role="dialog" aria-labelledby="appt-dialog-title">
      <div class="flex items-start justify-between gap-3 border-b border-line px-6 py-4">
        <div class="min-w-0">
          <span class="rounded-full px-2.5 py-0.5 text-xs font-semibold" [class]="badge[a.status]">{{ label[a.status] }}</span>
          <h2 id="appt-dialog-title" class="mt-2 truncate text-lg font-bold">{{ a.customerName }}</h2>
          <p class="text-sm text-muted">{{ a.serviceName }}</p>
        </div>
        <button type="button" class="btn-ghost px-2" (click)="close()" aria-label="Cerrar"><ng-icon name="heroXMark" size="20" /></button>
      </div>

      <div class="space-y-5 overflow-y-auto px-6 py-5">
        <dl class="space-y-3 text-sm">
          <div class="flex items-center gap-3">
            <dt class="sr-only">Cuándo</dt>
            <ng-icon name="heroClock" size="18" class="shrink-0 text-muted" />
            <dd><span class="font-semibold first-letter:uppercase">{{ when() }}</span> <span class="text-muted">({{ duration() }})</span></dd>
          </div>
          <div class="flex items-center gap-3">
            <dt class="sr-only">Con quién</dt>
            <span class="ml-0.5 size-3.5 shrink-0 rounded-full" [style.background-color]="a.staffColor" aria-hidden="true"></span>
            <dd>Con <strong>{{ a.staffName }}</strong> · {{ price() }}</dd>
          </div>
          <div class="flex items-center gap-3">
            <dt class="sr-only">Cliente</dt>
            <ng-icon name="heroUser" size="18" class="shrink-0 text-muted" />
            <dd class="flex flex-wrap gap-x-3">
              <a [href]="'https://wa.me/' + whatsApp(a.customerPhone)" target="_blank" rel="noopener noreferrer" class="text-accent hover:underline">
                {{ a.customerPhone }}<span class="sr-only"> (WhatsApp, se abre en otra pestaña)</span>
              </a>
              <a [routerLink]="['/app/clientes', a.customerId]" (click)="ref.close()" class="text-accent hover:underline">Ver cliente</a>
            </dd>
          </div>
          @if (a.customerNote) {
            <p class="rounded-xl bg-surface-2 px-3.5 py-2.5 text-sm text-muted">"{{ a.customerNote }}"</p>
          }
        </dl>

        @if (a.status === 'Confirmed' && !started()) {
          <div class="rounded-xl border border-line p-4">
            <p class="text-sm font-semibold">Avisar por WhatsApp</p>
            <p class="mt-0.5 text-xs text-muted">Se abre WhatsApp con el mensaje escrito y el enlace para que gestione su cita.</p>
            <div class="mt-3 grid gap-2 sm:grid-cols-2">
              @if (messages(); as m) {
                <a [href]="m.confirmation" target="_blank" rel="noopener noreferrer" class="btn-secondary">
                  <ng-icon name="heroChatBubbleLeftRight" size="17" /> Confirmación
                  <span class="sr-only">(se abre en otra pestaña)</span>
                </a>
                <a [href]="m.reminder" target="_blank" rel="noopener noreferrer" class="btn-secondary">
                  <ng-icon name="heroBellAlert" size="17" /> Recordatorio
                  <span class="sr-only">(se abre en otra pestaña)</span>
                </a>
              } @else {
                <span class="h-10 animate-pulse rounded-xl bg-surface-2 sm:col-span-2"></span>
              }
            </div>
          </div>
        }

        @if (error()) {
          <p class="rounded-xl border border-danger/40 bg-danger/10 px-3.5 py-2.5 text-sm text-danger" role="alert">{{ error() }}</p>
        }

        @if (moving()) {
          <form [formGroup]="moveForm" (ngSubmit)="move()" novalidate class="space-y-4 rounded-xl border border-line p-4">
            <p class="text-sm font-semibold">Mover la cita</p>
            <div>
              <label for="move-staff" class="field-label">Con</label>
              <select id="move-staff" formControlName="staffMemberId" class="field-input">
                @for (person of activeStaff(); track person.id) {
                  <option [value]="person.id">{{ person.name }}</option>
                }
              </select>
            </div>
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label for="move-date" class="field-label">Día</label>
                <input id="move-date" type="date" formControlName="date" class="field-input" />
              </div>
              <div>
                <label for="move-time" class="field-label">Hora</label>
                <input id="move-time" type="time" step="300" formControlName="time" class="field-input" />
              </div>
            </div>
            <div class="flex justify-end gap-2">
              <button type="button" class="btn-secondary" (click)="moving.set(false)">Volver</button>
              <button type="submit" class="btn-primary" [disabled]="busy() || moveForm.invalid">Mover</button>
            </div>
          </form>
        }
      </div>

      @if (!moving()) {
        <div class="flex flex-wrap justify-end gap-2 border-t border-line px-6 py-4">
          @switch (a.status) {
            @case ('Confirmed') {
              <button type="button" class="btn-ghost hover:text-danger" [disabled]="busy()" (click)="setStatus('Cancelled')">
                <ng-icon name="heroXCircle" size="18" /> Cancelar cita
              </button>
              <button type="button" class="btn-secondary" [disabled]="busy()" (click)="moving.set(true)">
                <ng-icon name="heroArrowsRightLeft" size="17" /> Mover
              </button>
              @if (started()) {
                <button type="button" class="btn-secondary" [disabled]="busy()" (click)="setStatus('NoShow')">
                  <ng-icon name="heroNoSymbol" size="17" /> No asistió
                </button>
                <button type="button" class="btn-primary" [disabled]="busy()" (click)="setStatus('Completed')">
                  <ng-icon name="heroCheckCircle" size="18" /> Atendida
                </button>
              }
            }
            @case ('Cancelled') {
              @if (!ended()) {
                <button type="button" class="btn-secondary" [disabled]="busy()" (click)="setStatus('Confirmed')">
                  <ng-icon name="heroArrowPath" size="17" /> Reactivar
                </button>
              }
            }
            @default {
              <button type="button" class="btn-secondary" [disabled]="busy()" (click)="setStatus('Confirmed')">
                <ng-icon name="heroArrowPath" size="17" /> Deshacer
              </button>
            }
          }
        </div>
      }
    </div>
  `,
})
export class AppointmentDialog {
  private readonly http = inject(HttpClient);
  private readonly data = inject<AppointmentDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<void>>(DialogRef);

  protected readonly appointment = signal(this.data.appointment);

  protected readonly label = STATUS_LABEL;
  protected readonly badge = STATUS_BADGE;
  protected readonly whatsApp = whatsAppNumber;
  protected readonly activeStaff = computed(() =>
    this.data.staff.filter((s) => s.isActive || s.id === this.appointment().staffMemberId),
  );

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly moving = signal(false);

  /** Enlace privado de la cita, pedido al abrir el diálogo para que los botones de WhatsApp sean enlaces normales. */
  private readonly manageLink = httpResource<{ token: string }>(() => `${API_BASE}/appointments/${this.appointment().id}/manage-link`);
  private readonly auth = inject(AuthService);

  protected readonly messages = computed(() => {
    if (!this.manageLink.hasValue()) return null;
    const a = this.appointment();
    const data = {
      customerName: a.customerName,
      businessName: this.auth.tenant()?.name ?? '',
      serviceName: a.serviceName,
      staffName: a.staffName,
      startsAt: a.startsAt,
      link: `${location.origin}/cita/${this.manageLink.value().token}`,
    };
    const number = whatsAppNumber(a.customerPhone);
    return { confirmation: whatsAppLink(number, confirmationMessage(data)), reminder: whatsAppLink(number, reminderMessage(data)) };
  });

  protected readonly moveForm = inject(NonNullableFormBuilder).group({
    staffMemberId: [this.data.appointment.staffMemberId, Validators.required],
    date: [this.data.appointment.startsAt.slice(0, 10), Validators.required],
    time: [this.data.appointment.startsAt.slice(11, 16), Validators.required],
  });

  protected readonly when = computed(() => {
    const a = this.appointment();
    const day = new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long' }).format(parseLocal(a.startsAt));
    return `${day}, ${formatTime(a.startsAt.slice(11, 16))} – ${formatTime(a.endsAt.slice(11, 16))}`;
  });
  protected readonly duration = computed(() => {
    const a = this.appointment();
    return formatDuration(Math.round((parseLocal(a.endsAt).getTime() - parseLocal(a.startsAt).getTime()) / 60_000));
  });
  protected readonly price = computed(() => formatPrice(this.appointment().price));

  /**
   * Se compara con la hora del navegador: para un negocio en otra zona horaria puede diferir, pero el servidor
   * valida igual (si aún no empezó, rechaza marcarla como atendida).
   */
  protected readonly started = computed(() => parseLocal(this.appointment().startsAt) <= new Date());
  protected readonly ended = computed(() => parseLocal(this.appointment().endsAt) <= new Date());

  protected setStatus(status: AppointmentStatus): void {
    this.send(this.http.patch<AgendaAppointment>(`${API_BASE}/appointments/${this.appointment().id}/status`, { status }, { context: silentErrors() }));
  }

  protected move(): void {
    if (this.moveForm.invalid) return;
    this.send(
      this.http.put<AgendaAppointment>(`${API_BASE}/appointments/${this.appointment().id}/move`, this.moveForm.getRawValue(), {
        context: silentErrors(),
      }),
      () => this.moving.set(false),
    );
  }

  protected close(): void {
    this.ref.close();
  }

  private send(request: Observable<AgendaAppointment>, after?: () => void): void {
    this.busy.set(true);
    this.error.set(null);
    request.subscribe({
      next: (updated) => {
        this.appointment.set(updated);
        this.busy.set(false);
        after?.();
      },
      error: (e: unknown) => {
        this.error.set(getErrorMessage(e));
        this.busy.set(false);
      },
    });
  }
}
