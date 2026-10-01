import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { API_BASE, getErrorMessage, getFieldErrors, silentErrors } from '../../../core/http/api';
import { parseLocal, toLocalIso } from '../../../shared/schedule';
import { StaffMember, TimeOff, TimeOffRequest } from '../business.models';

const today = (): string => toLocalIso(new Date()).slice(0, 10);

/** Arma las fechas que espera la API. Días completos = de las 00:00 del primero a las 00:00 del día siguiente al último. */
export function buildTimeOffRange(
  allDay: boolean,
  fromDate: string,
  fromTime: string,
  toDate: string,
  toTime: string,
): { startsAt: string; endsAt: string } {
  if (allDay) {
    const end = parseLocal(`${toDate}T00:00`);
    end.setDate(end.getDate() + 1);
    return { startsAt: `${fromDate}T00:00`, endsAt: toLocalIso(end) };
  }
  return { startsAt: `${fromDate}T${fromTime}`, endsAt: `${toDate}T${toTime}` };
}

@Component({
  selector: 'app-time-off-dialog',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form
      [formGroup]="form"
      (ngSubmit)="save()"
      novalidate
      class="card flex max-h-[calc(100dvh-2rem)] w-[min(30rem,calc(100vw-2rem))] flex-col shadow-2xl"
      aria-labelledby="time-off-title"
    >
      <div class="border-b border-line px-6 py-4">
        <h2 id="time-off-title" class="text-lg font-bold">Nuevo bloqueo</h2>
        <p class="mt-0.5 text-sm text-muted">En ese tiempo no se podrá reservar.</p>
      </div>

      <div class="space-y-5 overflow-y-auto px-6 py-5">
        @if (error()) {
          <p class="rounded-xl border border-danger/40 bg-danger/10 px-3.5 py-2.5 text-sm text-danger" role="alert">{{ error() }}</p>
        }

        <div>
          <label for="who" class="field-label">¿A quién aplica?</label>
          <select id="who" formControlName="staffMemberId" class="field-input">
            <option value="">Todo el negocio (cerrado)</option>
            @for (person of staff; track person.id) {
              <option [value]="person.id">{{ person.name }}</option>
            }
          </select>
        </div>

        <label class="flex cursor-pointer items-center gap-3">
          <input type="checkbox" formControlName="allDay" class="size-4 accent-violet-600" />
          <span class="text-sm font-medium">Días completos</span>
        </label>

        <div class="grid gap-4 sm:grid-cols-2">
          <div>
            <label for="fromDate" class="field-label">{{ values().allDay ? 'Desde el día' : 'Inicio' }}</label>
            <input id="fromDate" type="date" formControlName="fromDate" class="field-input" />
            @if (!values().allDay) {
              <label for="fromTime" class="sr-only">Hora de inicio</label>
              <input id="fromTime" type="time" step="300" formControlName="fromTime" class="field-input mt-2" />
            }
          </div>
          <div>
            <label for="toDate" class="field-label">{{ values().allDay ? 'Hasta el día (incluido)' : 'Fin' }}</label>
            <input id="toDate" type="date" formControlName="toDate" class="field-input" [attr.aria-invalid]="!!rangeError()" />
            @if (!values().allDay) {
              <label for="toTime" class="sr-only">Hora de fin</label>
              <input id="toTime" type="time" step="300" formControlName="toTime" class="field-input mt-2" />
            }
          </div>
        </div>
        @if (rangeError(); as message) {
          <p class="field-error -mt-2">{{ message }}</p>
        }

        <div>
          <label for="reason" class="field-label">Motivo <span class="font-normal text-faint">(opcional, solo lo ve tu equipo)</span></label>
          <input id="reason" formControlName="reason" placeholder="Ej. Vacaciones, festivo, capacitación" class="field-input" />
        </div>
      </div>

      <div class="flex justify-end gap-2 border-t border-line px-6 py-4">
        <button type="button" class="btn-secondary" (click)="ref.close()">Cancelar</button>
        <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Guardando…' : 'Guardar bloqueo' }}</button>
      </div>
    </form>
  `,
})
export class TimeOffDialog {
  private readonly http = inject(HttpClient);
  protected readonly staff = inject<StaffMember[]>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<TimeOff>>(DialogRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    staffMemberId: [''],
    allDay: [true],
    fromDate: [today(), [Validators.required]],
    fromTime: ['08:00', [Validators.required]],
    toDate: [today(), [Validators.required]],
    toTime: ['12:00', [Validators.required]],
    reason: ['', [Validators.maxLength(200)]],
  });

  protected readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly serverRangeError = signal<string | null>(null);

  constructor() {
    // Un error del servidor sobre las fechas deja de aplicar en cuanto el usuario las cambia.
    this.form.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.serverRangeError.set(null));
  }

  protected readonly rangeError = computed(() => {
    this.values(); // se recalcula con cada cambio del formulario
    const v = this.form.getRawValue();
    if (this.serverRangeError()) return this.serverRangeError();
    if (!v.fromDate || !v.toDate) return 'Elige las fechas.';
    const { startsAt, endsAt } = buildTimeOffRange(v.allDay, v.fromDate, v.fromTime, v.toDate, v.toTime);
    return parseLocal(endsAt) > parseLocal(startsAt) ? null : 'El fin debe ser después del inicio.';
  });

  protected save(): void {
    this.serverRangeError.set(null);
    if (this.form.invalid || this.rangeError()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body: TimeOffRequest = {
      staffMemberId: v.staffMemberId || null,
      ...buildTimeOffRange(v.allDay, v.fromDate, v.fromTime, v.toDate, v.toTime),
      reason: v.reason.trim() || null,
    };

    this.saving.set(true);
    this.error.set(null);
    this.http.post<TimeOff>(`${API_BASE}/time-off`, body, { context: silentErrors() }).subscribe({
      next: (saved) => this.ref.close(saved),
      error: (e: unknown) => {
        const fields = getFieldErrors(e);
        const range = fields['startsAt'] ?? fields['endsAt'];
        if (range) this.serverRangeError.set(range);
        else this.error.set(getErrorMessage(e));
        this.saving.set(false);
      },
    });
  }
}
