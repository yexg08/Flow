import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpClient, HttpParams, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroMagnifyingGlass, heroXMark } from '@ng-icons/heroicons/outline';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { API_BASE, getErrorMessage, getFieldErrors, silentErrors } from '../../../core/http/api';
import { formatDuration } from '../../../shared/format';
import { Service, StaffMember } from '../business.models';
import { AgendaAppointment } from './agenda.models';

export interface NewAppointmentData {
  services: Service[];
  staff: StaffMember[];
  date: string;
  time: string;
  staffId: string | null;
}

interface CustomerOption {
  id: string;
  name: string;
  phone: string;
}

/** Agendar desde el panel: una llamada o alguien que llega en persona. */
@Component({
  selector: 'app-new-appointment-dialog',
  imports: [ReactiveFormsModule, NgIcon],
  providers: [provideIcons({ heroMagnifyingGlass, heroXMark })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form
      [formGroup]="form"
      (ngSubmit)="save()"
      novalidate
      class="card flex max-h-[calc(100dvh-2rem)] w-[min(32rem,calc(100vw-2rem))] flex-col shadow-2xl"
      aria-labelledby="new-appt-title"
    >
      <div class="border-b border-line px-6 py-4">
        <h2 id="new-appt-title" class="text-lg font-bold">Nueva cita</h2>
      </div>

      <div class="space-y-5 overflow-y-auto px-6 py-5">
        @if (error()) {
          <p class="rounded-xl border border-danger/40 bg-danger/10 px-3.5 py-2.5 text-sm text-danger" role="alert">{{ error() }}</p>
        }

        <div>
          <label for="na-service" class="field-label">Servicio</label>
          <select id="na-service" formControlName="serviceId" class="field-input" [attr.aria-invalid]="touchedInvalid('serviceId')">
            <option value="" disabled>Elige un servicio</option>
            @for (service of services; track service.id) {
              <option [value]="service.id">{{ service.name }} · {{ duration(service.durationMinutes) }}</option>
            }
          </select>
        </div>

        <div>
          <label for="na-staff" class="field-label">Con</label>
          <select id="na-staff" formControlName="staffMemberId" class="field-input" [attr.aria-invalid]="touchedInvalid('staffMemberId')">
            <option value="" disabled>Elige quién atiende</option>
            @for (person of staffForService(); track person.id) {
              <option [value]="person.id">{{ person.name }}</option>
            }
          </select>
          @if (staffForService().length === 0 && values().serviceId) {
            <p class="field-error">Nadie del equipo tiene asignado este servicio.</p>
          }
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div>
            <label for="na-date" class="field-label">Día</label>
            <input id="na-date" type="date" formControlName="date" class="field-input" />
          </div>
          <div>
            <label for="na-time" class="field-label">Hora</label>
            <input id="na-time" type="time" step="300" formControlName="time" class="field-input" />
          </div>
        </div>

        <fieldset class="space-y-3">
          <legend class="field-label">Cliente</legend>
          @if (selected(); as customer) {
            <div class="flex items-center justify-between rounded-xl border border-accent bg-surface-2 px-3.5 py-2.5">
              <div>
                <p class="text-sm font-semibold">{{ customer.name }}</p>
                <p class="text-xs text-muted">{{ customer.phone }}</p>
              </div>
              <button type="button" class="btn-ghost px-2" (click)="selected.set(null)" aria-label="Quitar cliente"><ng-icon name="heroXMark" size="18" /></button>
            </div>
          } @else {
            <div class="relative">
              <ng-icon name="heroMagnifyingGlass" size="17" class="pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 text-faint" />
              <label for="na-search" class="sr-only">Buscar cliente existente por nombre o celular</label>
              <input
                id="na-search"
                type="search"
                #search
                (input)="term.set(search.value)"
                placeholder="Buscar cliente existente (nombre o celular)"
                class="field-input pl-10"
                autocomplete="off"
              />
            </div>
            @if (matches().length > 0) {
              <ul class="max-h-40 overflow-y-auto rounded-xl border border-line p-1" aria-label="Clientes encontrados">
                @for (c of matches(); track c.id) {
                  <li>
                    <button type="button" class="flex w-full cursor-pointer items-center justify-between rounded-lg px-3 py-2 text-left text-sm hover:bg-surface-2" (click)="selected.set(c)">
                      <span class="font-medium">{{ c.name }}</span>
                      <span class="text-xs text-muted">{{ c.phone }}</span>
                    </button>
                  </li>
                }
              </ul>
            }
            <p class="text-xs text-faint">O escribe los datos de un cliente nuevo:</p>
            <div class="grid gap-3 sm:grid-cols-2">
              <div>
                <label for="na-name" class="sr-only">Nombre del cliente nuevo</label>
                <input id="na-name" formControlName="customerName" placeholder="Nombre" class="field-input" [attr.aria-invalid]="!!serverErrors()['customerName']" />
              </div>
              <div>
                <label for="na-phone" class="sr-only">Celular del cliente nuevo</label>
                <input id="na-phone" type="tel" formControlName="customerPhone" placeholder="Celular" class="field-input" [attr.aria-invalid]="!!serverErrors()['customerPhone']" />
              </div>
            </div>
            @if (serverErrors()['customerName'] || serverErrors()['customerPhone']; as message) {
              <p class="field-error">{{ message }}</p>
            }
          }
        </fieldset>

        <div>
          <label for="na-note" class="field-label">Nota <span class="font-normal text-faint">(opcional)</span></label>
          <input id="na-note" formControlName="note" class="field-input" />
        </div>
      </div>

      <div class="flex justify-end gap-2 border-t border-line px-6 py-4">
        <button type="button" class="btn-secondary" (click)="ref.close()">Cancelar</button>
        <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Guardando…' : 'Agendar' }}</button>
      </div>
    </form>
  `,
})
export class NewAppointmentDialog {
  private readonly http = inject(HttpClient);
  private readonly data = inject<NewAppointmentData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<AgendaAppointment>>(DialogRef);

  protected readonly services = this.data.services.filter((s) => s.isActive);
  protected readonly duration = formatDuration;

  protected readonly form = inject(NonNullableFormBuilder).group({
    serviceId: ['', Validators.required],
    staffMemberId: [this.data.staffId ?? '', Validators.required],
    date: [this.data.date, Validators.required],
    time: [this.data.time, Validators.required],
    customerName: [''],
    customerPhone: [''],
    note: ['', Validators.maxLength(300)],
  });
  protected readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  protected readonly staffForService = computed(() => {
    const serviceId = this.values().serviceId;
    const active = this.data.staff.filter((s) => s.isActive);
    return serviceId ? active.filter((s) => s.serviceIds.includes(serviceId)) : active;
  });

  // Búsqueda de clientes existentes (con una pausa para no consultar en cada tecla).
  protected readonly term = signal('');
  private readonly debouncedTerm = toSignal(toObservable(this.term).pipe(debounceTime(250), distinctUntilChanged()), { initialValue: '' });
  private readonly found = httpResource<CustomerOption[]>(() => {
    const term = this.debouncedTerm().trim();
    return term.length >= 2 ? { url: `${API_BASE}/customers`, params: new HttpParams().set('search', term) } : undefined;
  });
  protected readonly matches = computed(() => (this.found.hasValue() && this.term().trim().length >= 2 ? this.found.value().slice(0, 8) : []));
  protected readonly selected = signal<CustomerOption | null>(null);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string>>({});

  protected touchedInvalid(field: 'serviceId' | 'staffMemberId'): boolean {
    const c = this.form.controls[field];
    return c.invalid && c.touched;
  }

  protected save(): void {
    const v = this.form.getRawValue();
    const customer = this.selected();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Completa el servicio, quién atiende, el día y la hora.');
      return;
    }
    if (!customer && (!v.customerName.trim() || !v.customerPhone.trim())) {
      this.error.set('Elige un cliente existente o escribe el nombre y el celular del nuevo.');
      return;
    }

    const body = {
      serviceId: v.serviceId,
      staffMemberId: v.staffMemberId,
      date: v.date,
      time: v.time,
      customerId: customer?.id ?? null,
      customerName: customer ? null : v.customerName.trim(),
      customerPhone: customer ? null : v.customerPhone.trim(),
      note: v.note.trim() || null,
    };

    this.saving.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    this.http.post<AgendaAppointment>(`${API_BASE}/appointments`, body, { context: silentErrors() }).subscribe({
      next: (created) => this.ref.close(created),
      error: (e: unknown) => {
        const fields = getFieldErrors(e);
        this.serverErrors.set(fields);
        this.error.set(Object.values(fields)[0] ?? getErrorMessage(e));
        this.saving.set(false);
      },
    });
  }
}
