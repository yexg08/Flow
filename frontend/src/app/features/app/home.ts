import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  heroArrowTopRightOnSquare,
  heroCalendarDays,
  heroCheck,
  heroClipboardDocument,
  heroLink,
} from '@ng-icons/heroicons/outline';
import { AuthService } from '../../core/auth/auth.service';
import { API_BASE } from '../../core/http/api';
import { ToastService } from '../../core/notifications/toast.service';
import { publicUrl } from '../../shared/slug';
import { whatsAppNumber } from '../../shared/format';
import { formatTime, parseLocal } from '../../shared/schedule';
import { BusinessSettings, Service, StaffMember, UpcomingAppointment } from './business.models';

@Component({
  selector: 'app-home',
  imports: [RouterLink, NgIcon],
  providers: [provideIcons({ heroArrowTopRightOnSquare, heroCalendarDays, heroCheck, heroClipboardDocument, heroLink })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-4xl">
      <h1 class="text-2xl font-extrabold tracking-tight sm:text-3xl">Hola, {{ firstName() }}</h1>
      <p class="mt-1 text-muted">Esto es lo que necesitas para que tus clientes empiecen a reservar.</p>

      @if (auth.tenant(); as tenant) {
        <section class="card relative mt-8 overflow-hidden p-6" aria-labelledby="link-title">
          <div aria-hidden="true" class="bg-brand absolute inset-0 opacity-[0.07]"></div>
          <div class="relative flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div class="min-w-0">
              <h2 id="link-title" class="flex items-center gap-2 text-sm font-semibold text-muted">
                <ng-icon name="heroLink" size="16" class="text-accent" /> Tu enlace público
              </h2>
              <p class="mt-1 truncate text-lg font-bold">{{ link() }}</p>
            </div>
            <div class="flex shrink-0 gap-2">
              <button type="button" class="btn-secondary" (click)="copyLink()">
                <ng-icon [name]="copied() ? 'heroCheck' : 'heroClipboardDocument'" size="18" />
                {{ copied() ? 'Copiado' : 'Copiar' }}
              </button>
              <a [href]="'/n/' + tenant.slug" target="_blank" rel="noopener" class="btn-primary">
                Abrir <ng-icon name="heroArrowTopRightOnSquare" size="16" />
                <span class="sr-only">(se abre en otra pestaña)</span>
              </a>
            </div>
          </div>
        </section>
      }

      <section class="mt-8" aria-labelledby="checklist-title">
        <h2 id="checklist-title" class="text-lg font-bold">Primeros pasos</h2>
        <ol class="mt-4 space-y-3">
          @for (step of steps(); track step.title) {
            <li class="card flex items-center gap-4 p-4">
              <span
                class="flex size-8 shrink-0 items-center justify-center rounded-full border text-sm font-bold"
                [class]="step.done ? 'bg-brand border-transparent text-white' : 'border-line text-muted'"
              >
                @if (step.done) {
                  <ng-icon name="heroCheck" size="16" />
                  <span class="sr-only">Hecho:</span>
                } @else {
                  {{ $index + 1 }}
                }
              </span>
              <div class="min-w-0 flex-1">
                <p class="font-semibold" [class.text-muted]="step.done">{{ step.title }}</p>
                <p class="text-sm text-muted">{{ step.text }}</p>
              </div>
              @if (step.link && !step.done) {
                <a [routerLink]="step.link" class="btn-secondary shrink-0">{{ step.action }}</a>
              }
            </li>
          }
        </ol>
      </section>

      <section class="mt-10" aria-labelledby="upcoming-title">
        <h2 id="upcoming-title" class="flex items-center gap-2 text-lg font-bold">
          <ng-icon name="heroCalendarDays" size="20" class="text-accent" /> Próximas citas
        </h2>
        @if (upcoming.isLoading() && upcoming.value().length === 0) {
          <div class="card mt-4 h-28 animate-pulse bg-surface-2"></div>
        } @else if (upcoming.value().length === 0) {
          <p class="card mt-4 border-dashed p-6 text-center text-sm text-muted">
            Aún no tienes citas. Cuando alguien reserve desde tu enlace, aparecerá aquí.
          </p>
        } @else {
          <ul class="card mt-4 divide-y divide-line">
            @for (appt of upcoming.value(); track appt.id) {
              <li class="flex items-center gap-4 p-4">
                <div class="w-20 shrink-0 text-center">
                  <p class="text-xs font-semibold text-muted capitalize">{{ dayLabel(appt.startsAt) }}</p>
                  <p class="text-lg font-extrabold tabular-nums">{{ timeLabel(appt.startsAt) }}</p>
                </div>
                <span class="h-10 w-1 shrink-0 rounded-full" [style.background-color]="appt.staffColor" aria-hidden="true"></span>
                <div class="min-w-0 flex-1">
                  <p class="truncate font-semibold">{{ appt.customerName }} · {{ appt.serviceName }}</p>
                  <p class="truncate text-sm text-muted">
                    Con {{ appt.staffName }} ·
                    <a [href]="'https://wa.me/' + whatsApp(appt.customerPhone)" target="_blank" rel="noopener noreferrer" class="text-accent hover:underline">
                      {{ appt.customerPhone }}<span class="sr-only"> (WhatsApp, se abre en otra pestaña)</span>
                    </a>
                  </p>
                  @if (appt.customerNote) {
                    <p class="mt-1 truncate text-xs text-faint" [title]="appt.customerNote">"{{ appt.customerNote }}"</p>
                  }
                </div>
              </li>
            }
          </ul>
        }
      </section>
    </div>
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastService);

  private readonly services = httpResource<Service[]>(() => `${API_BASE}/services`);
  private readonly business = httpResource<BusinessSettings>(() => `${API_BASE}/business`);
  private readonly staff = httpResource<StaffMember[]>(() => `${API_BASE}/staff`);
  protected readonly upcoming = httpResource<UpcomingAppointment[]>(() => `${API_BASE}/appointments/upcoming?limit=10`, { defaultValue: [] });

  /** "hoy", "mañana" o "lun 5 oct". */
  protected dayLabel(startsAt: string): string {
    const date = parseLocal(startsAt);
    const today = new Date();
    const diff = Math.round(
      (new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime() -
        new Date(today.getFullYear(), today.getMonth(), today.getDate()).getTime()) /
        86_400_000,
    );
    if (diff === 0) return 'hoy';
    if (diff === 1) return 'mañana';
    return new Intl.DateTimeFormat('es-CO', { weekday: 'short', day: 'numeric', month: 'short' }).format(date).replace(/\./g, '');
  }

  protected readonly whatsApp = whatsAppNumber;

  protected timeLabel(startsAt: string): string {
    return formatTime(startsAt.slice(11, 16));
  }

  protected readonly copied = signal(false);
  protected readonly firstName = computed(() => this.auth.user()?.fullName.split(' ')[0] ?? '');
  protected readonly link = computed(() => {
    const tenant = this.auth.tenant();
    return tenant ? publicUrl(tenant.slug) : '';
  });

  protected readonly steps = computed(() => {
    const serviceCount = this.services.hasValue() ? this.services.value().filter((s) => s.isActive).length : 0;
    const hasWhatsApp = this.business.hasValue() && !!this.business.value().whatsApp;
    const scheduled = this.staff.hasValue() ? this.staff.value().filter((s) => s.isActive && s.workingHours.length > 0).length : 0;
    const owner = this.auth.isOwner();
    return [
      { title: 'Crea tu negocio', text: 'Ya tienes tu cuenta y tu enlace.', done: true, link: null, action: '' },
      {
        title: 'Agrega tus servicios',
        text: serviceCount > 0 ? `Tienes ${serviceCount} servicio(s) visibles en tu página.` : 'Lo que ofreces, con su duración y precio.',
        done: serviceCount > 0,
        link: '/app/servicios',
        action: 'Agregar',
      },
      {
        title: 'Arma tu equipo y sus horarios',
        text:
          scheduled > 0
            ? `${scheduled} persona(s) con horario para recibir reservas.`
            : 'Quién atiende y en qué horario (si trabajas solo, agrégate a ti).',
        done: scheduled > 0,
        link: '/app/equipo',
        action: owner ? 'Configurar' : 'Ver',
      },
      {
        title: 'Conecta tu WhatsApp',
        text: 'Para que tus clientes te escriban desde tu página.',
        done: hasWhatsApp,
        link: owner ? '/app/ajustes' : null,
        action: 'Configurar',
      },
    ];
  });

  protected async copyLink(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.link());
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    } catch {
      this.toasts.error('No se pudo copiar. Selecciona el enlace y cópialo a mano.');
    }
  }
}
