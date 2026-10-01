import { DialogRef, DIALOG_DATA } from '@angular/cdk/dialog';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { API_BASE, getErrorMessage, getFieldErrors, silentErrors } from '../../../core/http/api';
import { formatDuration } from '../../../shared/format';
import { Service, ServiceUpsert } from '../business.models';

const DURATIONS = [15, 20, 30, 45, 60, 75, 90, 120, 150, 180, 240];

/** Crear o editar un servicio. Devuelve el servicio guardado al cerrar, o nada si se cancela. */
@Component({
  selector: 'app-service-dialog',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form
      [formGroup]="form"
      (ngSubmit)="save()"
      novalidate
      class="card flex max-h-[calc(100dvh-2rem)] w-[min(32rem,calc(100vw-2rem))] flex-col shadow-2xl"
      aria-labelledby="service-dialog-title"
    >
      <div class="border-b border-line px-6 py-4">
        <h2 id="service-dialog-title" class="text-lg font-bold">{{ data ? 'Editar servicio' : 'Nuevo servicio' }}</h2>
      </div>

      <div class="space-y-5 overflow-y-auto px-6 py-5">
        @if (error()) {
          <p class="rounded-xl border border-danger/40 bg-danger/10 px-3.5 py-2.5 text-sm text-danger" role="alert">{{ error() }}</p>
        }
        <div>
          <label for="name" class="field-label">Nombre</label>
          <input id="name" formControlName="name" placeholder="Ej. Corte de cabello" class="field-input" [attr.aria-invalid]="invalid('name')" />
          @if (invalid('name')) {
            <p class="field-error">{{ serverErrors()['name'] ?? 'Escribe el nombre del servicio.' }}</p>
          }
        </div>

        <div>
          <label for="description" class="field-label">Descripción <span class="font-normal text-faint">(opcional)</span></label>
          <textarea id="description" formControlName="description" rows="3" class="field-input resize-none"></textarea>
        </div>

        <div class="grid gap-5 sm:grid-cols-2">
          <div>
            <label for="durationMinutes" class="field-label">Duración</label>
            <select id="durationMinutes" formControlName="durationMinutes" class="field-input">
              @for (minutes of durations; track minutes) {
                <option [ngValue]="minutes">{{ label(minutes) }}</option>
              }
            </select>
          </div>
          <div>
            <label for="price" class="field-label">Precio (COP)</label>
            <input id="price" type="number" inputmode="numeric" min="0" step="500" formControlName="price" class="field-input" [attr.aria-invalid]="invalid('price')" />
            @if (invalid('price')) {
              <p class="field-error">{{ serverErrors()['price'] ?? 'Escribe un precio válido (0 si es gratis).' }}</p>
            }
          </div>
        </div>

        <label class="flex cursor-pointer items-start gap-3 rounded-xl border border-line p-3.5">
          <input type="checkbox" formControlName="isActive" class="mt-0.5 size-4 accent-violet-600" />
          <span>
            <span class="block text-sm font-medium">Visible en mi página</span>
            <span class="block text-xs text-muted">Si lo desactivas, tus clientes no lo verán, pero no se borra.</span>
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
export class ServiceDialog {
  private readonly http = inject(HttpClient);
  protected readonly data = inject<Service | null>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<Service>>(DialogRef);

  protected readonly durations = DURATIONS.includes(this.data?.durationMinutes ?? 30)
    ? DURATIONS
    : [...DURATIONS, this.data!.durationMinutes].sort((a, b) => a - b);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.data?.name ?? '', [Validators.required, Validators.maxLength(100)]],
    description: [this.data?.description ?? '', [Validators.maxLength(500)]],
    durationMinutes: [this.data?.durationMinutes ?? 30, [Validators.required]],
    price: [this.data?.price ?? 0, [Validators.required, Validators.min(0), Validators.max(100_000_000)]],
    isActive: [this.data?.isActive ?? true],
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string>>({});

  protected readonly label = formatDuration;

  protected invalid(control: 'name' | 'price'): boolean {
    const c = this.form.controls[control];
    return (c.invalid && c.touched) || !!this.serverErrors()[control];
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const body: ServiceUpsert = { ...raw, description: raw.description.trim() || null };
    const request = this.data
      ? this.http.put<Service>(`${API_BASE}/services/${this.data.id}`, body, { context: silentErrors() })
      : this.http.post<Service>(`${API_BASE}/services`, body, { context: silentErrors() });

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: (saved) => this.ref.close(saved),
      error: (e: unknown) => {
        const fields = getFieldErrors(e);
        this.serverErrors.set(fields);
        if (Object.keys(fields).length === 0) this.error.set(getErrorMessage(e));
        this.saving.set(false);
      },
    });
  }
}
