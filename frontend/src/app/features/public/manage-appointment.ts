import { HttpClient, HttpErrorResponse, HttpParams, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  heroArrowPath,
  heroCalendarDays,
  heroChatBubbleLeftRight,
  heroCheck,
  heroCheckCircle,
  heroClipboardDocument,
  heroClock,
  heroXCircle,
} from '@ng-icons/heroicons/outline';
import { API_BASE, getErrorMessage, silentErrors } from '../../core/http/api';
import { ToastService } from '../../core/notifications/toast.service';
import { ConfirmService } from '../../shared/confirm';
import { formatDuration, formatPrice, readableTextOn } from '../../shared/format';
import { Logo } from '../../shared/logo';
import { formatTime, parseLocal } from '../../shared/schedule';
import { ThemeToggle } from '../../shared/theme-toggle';
import { DayStrip, SlotGrid, nextDays } from './booking-widgets';
import { PublicAppointment, PublicAvailability } from './public.models';

/** La cita de un cliente, a la que llega con el enlace privado que recibió al reservar. */
@Component({
  selector: 'app-manage-appointment',
  imports: [RouterLink, NgIcon, Logo, ThemeToggle, DayStrip, SlotGrid],
  providers: [
    provideIcons({
      heroArrowPath,
      heroCalendarDays,
      heroChatBubbleLeftRight,
      heroCheck,
      heroCheckCircle,
      heroClipboardDocument,
      heroClock,
      heroXCircle,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './manage-appointment.html',
})
export class ManageAppointment {
  private readonly http = inject(HttpClient);
  private readonly confirm = inject(ConfirmService);
  private readonly toasts = inject(ToastService);

  /** Vienen de la ruta cita/:token y del query ?nueva=1 (recién reservada). */
  readonly token = input.required<string>();
  readonly nueva = input<string>();

  protected readonly appointment = httpResource<PublicAppointment>(() => `${API_BASE}/public/appointments/${encodeURIComponent(this.token())}`);
  protected readonly accent = computed(() => (this.appointment.hasValue() ? this.appointment.value().accentColor : '#7c3aed'));
  protected readonly accentText = computed(() => readableTextOn(this.accent()));

  protected readonly when = computed(() => {
    if (!this.appointment.hasValue()) return null;
    const a = this.appointment.value();
    const start = parseLocal(a.startsAt);
    return {
      day: new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(start),
      time: `${formatTime(a.startsAt.slice(11, 16))} – ${formatTime(a.endsAt.slice(11, 16))}`,
    };
  });

  protected readonly whatsAppUrl = computed(() => {
    if (!this.appointment.hasValue() || !this.appointment.value().businessWhatsApp) return null;
    const a = this.appointment.value();
    const text = encodeURIComponent(`Hola, soy ${a.customerName}. Te escribo por mi cita de ${a.serviceName}.`);
    return `https://wa.me/${a.businessWhatsApp}?text=${text}`;
  });

  // Reprogramar
  protected readonly rescheduling = signal(false);
  protected readonly days = nextDays(14);
  protected readonly newDate = signal(this.days[0].date);
  protected readonly newTime = signal<string | null>(null);
  protected readonly availability = httpResource<PublicAvailability>(() =>
    this.rescheduling()
      ? {
          url: `${API_BASE}/public/appointments/${encodeURIComponent(this.token())}/availability`,
          params: new HttpParams().set('date', this.newDate()),
        }
      : undefined,
  );
  protected readonly slots = computed(() => (this.availability.hasValue() ? this.availability.value().slots : []));
  protected readonly busy = signal(false);
  protected readonly copied = signal(false);

  protected readonly formatPrice = formatPrice;
  protected readonly formatDuration = formatDuration;

  protected pickDate(date: string): void {
    this.newDate.set(date);
    this.newTime.set(null);
  }

  protected reschedule(): void {
    const time = this.newTime();
    if (!time) return;
    this.busy.set(true);
    this.http
      .post<PublicAppointment>(
        `${API_BASE}/public/appointments/${encodeURIComponent(this.token())}/reschedule`,
        { date: this.newDate(), time },
        { context: silentErrors() },
      )
      .subscribe({
        next: (updated) => {
          this.appointment.set(updated);
          this.rescheduling.set(false);
          this.newTime.set(null);
          this.busy.set(false);
          this.toasts.success('Listo, tu cita cambió de horario.');
        },
        error: (e: unknown) => {
          this.busy.set(false);
          if (e instanceof HttpErrorResponse && e.status === 409) {
            this.newTime.set(null);
            this.availability.reload();
          }
          this.toasts.error(getErrorMessage(e));
        },
      });
  }

  protected cancel(): void {
    this.confirm
      .ask({
        title: '¿Cancelar tu cita?',
        message: 'El horario quedará libre para otra persona. Si quieres otra hora, mejor reprograma.',
        confirmText: 'Sí, cancelar',
        danger: true,
      })
      .subscribe((yes) => {
        if (!yes) return;
        this.busy.set(true);
        this.http
          .post<PublicAppointment>(`${API_BASE}/public/appointments/${encodeURIComponent(this.token())}/cancel`, null, {
            context: silentErrors(),
          })
          .subscribe({
            next: (updated) => {
              this.appointment.set(updated);
              this.busy.set(false);
              this.toasts.success('Tu cita quedó cancelada.');
            },
            error: (e: unknown) => {
              this.busy.set(false);
              this.toasts.error(getErrorMessage(e));
            },
          });
      });
  }

  protected async copyLink(): Promise<void> {
    try {
      await navigator.clipboard.writeText(location.href.split('?')[0]);
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      this.toasts.error('No se pudo copiar. Guarda esta página en tus favoritos.');
    }
  }
}
