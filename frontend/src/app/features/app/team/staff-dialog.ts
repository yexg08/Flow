import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroCheck } from '@ng-icons/heroicons/outline';
import { API_BASE, getErrorMessage, getFieldErrors, silentErrors } from '../../../core/http/api';
import { COLOR_PRESETS } from '../../../shared/colors';
import { formatDuration, readableTextOn } from '../../../shared/format';
import { Service, StaffMember, StaffUpsert } from '../business.models';

export interface StaffDialogData {
  staff: StaffMember | null;
  services: Service[];
}

/** Crear o editar a una persona del equipo y elegir los servicios que hace. */
@Component({
  selector: 'app-staff-dialog',
  imports: [ReactiveFormsModule, NgIcon],
  providers: [provideIcons({ heroCheck })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form
      [formGroup]="form"
      (ngSubmit)="save()"
      novalidate
      class="card flex max-h-[calc(100dvh-2rem)] w-[min(32rem,calc(100vw-2rem))] flex-col shadow-2xl"
      aria-labelledby="staff-dialog-title"
    >
      <div class="border-b border-line px-6 py-4">
        <h2 id="staff-dialog-title" class="text-lg font-bold">{{ data.staff ? 'Editar persona' : 'Nueva persona del equipo' }}</h2>
      </div>

      <div class="space-y-5 overflow-y-auto px-6 py-5">
        @if (error()) {
          <p class="rounded-xl border border-danger/40 bg-danger/10 px-3.5 py-2.5 text-sm text-danger" role="alert">{{ error() }}</p>
        }

        <div>
          <label for="staff-name" class="field-label">Nombre</label>
          <input id="staff-name" formControlName="name" placeholder="Ej. Andrés" class="field-input" [attr.aria-invalid]="nameInvalid()" />
          @if (nameInvalid()) {
            <p class="field-error">{{ serverErrors()['name'] ?? 'Escribe el nombre.' }}</p>
          }
        </div>

        <fieldset>
          <legend class="field-label">Color en la agenda</legend>
          <div class="flex flex-wrap gap-2" role="radiogroup" aria-label="Color">
            @for (preset of presets; track preset.color) {
              <button
                type="button"
                role="radio"
                class="flex size-9 cursor-pointer items-center justify-center rounded-full ring-offset-2 ring-offset-surface transition hover:scale-110"
                [style.background-color]="preset.color"
                [class.ring-2]="color() === preset.color"
                [class.ring-accent]="color() === preset.color"
                [attr.aria-checked]="color() === preset.color"
                [attr.aria-label]="preset.name"
                [title]="preset.name"
                (click)="form.controls.color.setValue(preset.color)"
              >
                @if (color() === preset.color) {
                  <ng-icon name="heroCheck" size="16" [style.color]="textOn(preset.color)" />
                }
              </button>
            }
          </div>
        </fieldset>

        <fieldset>
          <legend class="field-label">Servicios que hace</legend>
          @if (data.services.length === 0) {
            <p class="rounded-xl border border-dashed border-line px-3.5 py-3 text-sm text-muted">
              Aún no tienes servicios. Puedes asignárselos después de crearlos.
            </p>
          } @else {
            <ul class="max-h-56 space-y-1 overflow-y-auto rounded-xl border border-line p-1.5">
              @for (service of data.services; track service.id) {
                <li>
                  <label class="flex cursor-pointer items-center gap-3 rounded-lg px-2.5 py-2 hover:bg-surface-2">
                    <input
                      type="checkbox"
                      class="size-4 accent-violet-600"
                      [checked]="selected().has(service.id)"
                      (change)="toggleService(service.id)"
                    />
                    <span class="flex-1 text-sm">{{ service.name }}</span>
                    <span class="text-xs text-faint">{{ duration(service.durationMinutes) }}</span>
                  </label>
                </li>
              }
            </ul>
            <div class="mt-2 flex gap-3 text-xs">
              <button type="button" class="font-semibold text-accent hover:underline" (click)="selectAll()">Todos</button>
              <button type="button" class="font-semibold text-accent hover:underline" (click)="clearAll()">Ninguno</button>
            </div>
          }
        </fieldset>

        <label class="flex cursor-pointer items-start gap-3 rounded-xl border border-line p-3.5">
          <input type="checkbox" formControlName="isActive" class="mt-0.5 size-4 accent-violet-600" />
          <span>
            <span class="block text-sm font-medium">Recibe reservas</span>
            <span class="block text-xs text-muted">Desactívalo si ya no trabaja aquí o se fue por un tiempo largo. No se borra nada.</span>
          </span>
        </label>
      </div>

      <div class="flex justify-end gap-2 border-t border-line px-6 py-4">
        <button type="button" class="btn-secondary" (click)="ref.close()">Cancelar</button>
        <button type="submit" class="btn-primary" [disabled]="saving()">{{ saving() ? 'Guardando…' : 'Guardar' }}</button>
      </div>
    </form>
  `,
})
export class StaffDialog {
  private readonly http = inject(HttpClient);
  protected readonly data = inject<StaffDialogData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<StaffMember>>(DialogRef);

  protected readonly presets = COLOR_PRESETS;
  protected readonly duration = formatDuration;
  protected readonly textOn = readableTextOn;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data.staff?.name ?? '', [Validators.required, Validators.maxLength(80)]],
    color: [this.data.staff?.color ?? COLOR_PRESETS[0].color],
    isActive: [this.data.staff?.isActive ?? true],
  });

  /** Una persona nueva arranca haciendo todos los servicios: es lo más común en negocios pequeños. */
  protected readonly selected = signal(
    new Set(this.data.staff ? this.data.staff.serviceIds : this.data.services.map((s) => s.id)),
  );
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string>>({});
  protected readonly color = toSignal(this.form.controls.color.valueChanges, { initialValue: this.form.controls.color.value });

  protected nameInvalid(): boolean {
    const c = this.form.controls.name;
    return (c.invalid && c.touched) || !!this.serverErrors()['name'];
  }

  protected toggleService(id: string): void {
    this.selected.update((set) => {
      const next = new Set(set);
      if (!next.delete(id)) next.add(id);
      return next;
    });
  }

  protected selectAll(): void {
    this.selected.set(new Set(this.data.services.map((s) => s.id)));
  }

  protected clearAll(): void {
    this.selected.set(new Set());
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const body: StaffUpsert = { name: raw.name.trim(), color: raw.color, isActive: raw.isActive, serviceIds: [...this.selected()] };
    const request = this.data.staff
      ? this.http.put<StaffMember>(`${API_BASE}/staff/${this.data.staff.id}`, body, { context: silentErrors() })
      : this.http.post<StaffMember>(`${API_BASE}/staff`, body, { context: silentErrors() });

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: (saved) => this.ref.close(saved),
      error: (e: unknown) => {
        const fields = getFieldErrors(e);
        this.serverErrors.set(fields);
        if (Object.keys(fields).length === 0 || fields['serviceIds']) this.error.set(fields['serviceIds'] ?? getErrorMessage(e));
        this.saving.set(false);
      },
    });
  }
}
