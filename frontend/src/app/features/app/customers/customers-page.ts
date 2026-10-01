import { HttpParams, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroChevronRight, heroMagnifyingGlass, heroUserGroup } from '@ng-icons/heroicons/outline';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { API_BASE } from '../../../core/http/api';
import { formatDate } from '../../../shared/format';
import { formatTime } from '../../../shared/schedule';

export interface CustomerSummary {
  id: string;
  name: string;
  phone: string;
  email: string | null;
  visits: number;
  noShows: number;
  lastVisitAt: string | null;
  nextAppointmentAt: string | null;
}

@Component({
  selector: 'app-customers-page',
  imports: [RouterLink, NgIcon],
  providers: [provideIcons({ heroChevronRight, heroMagnifyingGlass, heroUserGroup })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-4xl">
      <h1 class="text-2xl font-extrabold tracking-tight sm:text-3xl">Clientes</h1>
      <p class="mt-1 text-muted">Quienes han reservado o agendado contigo, con su historial.</p>

      <div class="relative mt-6 max-w-md">
        <ng-icon name="heroMagnifyingGlass" size="18" class="pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 text-faint" />
        <label for="customer-search" class="sr-only">Buscar cliente por nombre o celular</label>
        <input
          id="customer-search"
          type="search"
          #search
          (input)="term.set(search.value)"
          placeholder="Buscar por nombre o celular"
          class="field-input pl-10"
        />
      </div>

      @if (customers.isLoading() && customers.value().length === 0) {
        <div class="card mt-4 h-48 animate-pulse bg-surface-2"></div>
      } @else if (customers.error()) {
        <div class="card mt-4 p-8 text-center">
          <p class="font-semibold">No se pudo cargar la lista.</p>
          <button type="button" class="btn-secondary mt-4" (click)="customers.reload()">Reintentar</button>
        </div>
      } @else if (customers.value().length === 0) {
        <div class="card mt-4 flex flex-col items-center px-6 py-14 text-center">
          <div class="flex size-14 items-center justify-center rounded-2xl bg-violet-500/15 text-accent">
            <ng-icon name="heroUserGroup" size="28" />
          </div>
          <h2 class="mt-5 text-lg font-bold">{{ term() ? 'Nadie coincide con la búsqueda' : 'Aún no tienes clientes' }}</h2>
          @if (!term()) {
            <p class="mt-1 max-w-sm text-sm text-muted">Aparecerán aquí cuando alguien reserve desde tu página o agendes desde la agenda.</p>
          }
        </div>
      } @else {
        <ul class="card mt-4 divide-y divide-line">
          @for (c of customers.value(); track c.id) {
            <li>
              <a [routerLink]="[c.id]" class="flex items-center gap-4 px-4 py-3.5 transition hover:bg-surface-2 sm:px-5">
                <span class="bg-brand flex size-10 shrink-0 items-center justify-center rounded-full font-bold text-white" aria-hidden="true">
                  {{ c.name.charAt(0).toUpperCase() }}
                </span>
                <div class="min-w-0 flex-1">
                  <p class="truncate font-semibold">{{ c.name }}</p>
                  <p class="truncate text-sm text-muted">
                    {{ c.phone }}
                    @if (c.nextAppointmentAt) {
                      · Próxima: {{ when(c.nextAppointmentAt) }}
                    } @else if (c.lastVisitAt) {
                      · Última visita: {{ date(c.lastVisitAt) }}
                    }
                  </p>
                </div>
                <div class="hidden shrink-0 text-right text-sm sm:block">
                  <p class="font-semibold tabular-nums">{{ c.visits }} visita(s)</p>
                  @if (c.noShows > 0) {
                    <p class="text-xs font-semibold text-danger">{{ c.noShows }} inasistencia(s)</p>
                  }
                </div>
                <ng-icon name="heroChevronRight" size="18" class="shrink-0 text-faint" />
              </a>
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class CustomersPage {
  protected readonly term = signal('');
  private readonly debounced = toSignal(toObservable(this.term).pipe(debounceTime(250), distinctUntilChanged()), { initialValue: '' });

  protected readonly customers = httpResource<CustomerSummary[]>(
    () => ({ url: `${API_BASE}/customers`, params: new HttpParams().set('search', this.debounced().trim()) }),
    { defaultValue: [] },
  );

  protected readonly date = formatDate;

  protected when(localIso: string): string {
    return `${formatDate(localIso)}, ${formatTime(localIso.slice(11, 16))}`;
  }
}
