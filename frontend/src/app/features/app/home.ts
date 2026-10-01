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
import { BusinessSettings, Service } from './business.models';

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

      <section class="card mt-8 flex items-start gap-4 border-dashed p-5">
        <ng-icon name="heroCalendarDays" size="24" class="shrink-0 text-accent" />
        <div>
          <h2 class="font-semibold">Próximamente: horarios, agenda y reservas</h2>
          <p class="mt-1 text-sm text-muted">
            En las siguientes versiones podrás definir los horarios de tu equipo y tus clientes reservarán desde tu enlace.
          </p>
        </div>
      </section>
    </div>
  `,
})
export class Home {
  protected readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastService);

  private readonly services = httpResource<Service[]>(() => `${API_BASE}/services`);
  private readonly business = httpResource<BusinessSettings>(() => `${API_BASE}/business`);

  protected readonly copied = signal(false);
  protected readonly firstName = computed(() => this.auth.user()?.fullName.split(' ')[0] ?? '');
  protected readonly link = computed(() => {
    const tenant = this.auth.tenant();
    return tenant ? publicUrl(tenant.slug) : '';
  });

  protected readonly steps = computed(() => {
    const serviceCount = this.services.hasValue() ? this.services.value().filter((s) => s.isActive).length : 0;
    const hasWhatsApp = this.business.hasValue() && !!this.business.value().whatsApp;
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
