import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroCheck, heroExclamationTriangle } from '@ng-icons/heroicons/outline';
import { AuthService } from '../../../core/auth/auth.service';
import { API_BASE, getErrorMessage, getFieldErrors, silentErrors } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { readableTextOn } from '../../../shared/format';
import { hasValidSlugFormat } from '../../../shared/slug';
import { BusinessSettings, UpdateBusiness } from '../business.models';

export const TIME_ZONES = [
  { id: 'America/Bogota', label: 'Colombia (Bogotá)' },
  { id: 'America/Lima', label: 'Perú (Lima)' },
  { id: 'America/Guayaquil', label: 'Ecuador (Guayaquil)' },
  { id: 'America/Panama', label: 'Panamá' },
  { id: 'America/Mexico_City', label: 'México (Ciudad de México)' },
  { id: 'America/Caracas', label: 'Venezuela (Caracas)' },
  { id: 'America/La_Paz', label: 'Bolivia (La Paz)' },
  { id: 'America/Santiago', label: 'Chile (Santiago)' },
  { id: 'America/Argentina/Buenos_Aires', label: 'Argentina (Buenos Aires)' },
  { id: 'America/Montevideo', label: 'Uruguay (Montevideo)' },
  { id: 'America/Asuncion', label: 'Paraguay (Asunción)' },
  { id: 'America/Costa_Rica', label: 'Costa Rica' },
  { id: 'America/Guatemala', label: 'Guatemala' },
  { id: 'America/Santo_Domingo', label: 'República Dominicana' },
  { id: 'Europe/Madrid', label: 'España (Madrid)' },
];

export const ACCENT_PRESETS = [
  { color: '#a855f7', name: 'Violeta' },
  { color: '#ec4899', name: 'Rosa' },
  { color: '#6366f1', name: 'Índigo' },
  { color: '#0ea5e9', name: 'Celeste' },
  { color: '#14b8a6', name: 'Turquesa' },
  { color: '#22c55e', name: 'Verde' },
  { color: '#f59e0b', name: 'Ámbar' },
  { color: '#ef4444', name: 'Rojo' },
  { color: '#1f2937', name: 'Grafito' },
];

type Field = 'name' | 'slug' | 'timeZone' | 'whatsApp' | 'accentColor';

@Component({
  selector: 'app-settings',
  imports: [ReactiveFormsModule, NgIcon],
  providers: [provideIcons({ heroCheck, heroExclamationTriangle })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './settings.html',
})
export class Settings {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly toasts = inject(ToastService);

  protected readonly business = httpResource<BusinessSettings>(() => `${API_BASE}/business`);
  protected readonly timeZones = TIME_ZONES;
  protected readonly presets = ACCENT_PRESETS;
  protected readonly host = location.host;

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(80)]],
    slug: ['', [Validators.required]],
    timeZone: ['America/Bogota', [Validators.required]],
    whatsApp: ['', [Validators.pattern(/^[0-9]{10,15}$/)]],
    accentColor: ['#a855f7', [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)]],
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string>>({});

  private readonly values = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });
  protected readonly accent = computed(() => {
    const color = this.values().accentColor ?? '#a855f7';
    return /^#[0-9a-fA-F]{6}$/.test(color) ? color : '#a855f7';
  });
  protected readonly accentText = computed(() => readableTextOn(this.accent()));
  protected readonly slugChanged = computed(() => this.business.hasValue() && this.values().slug !== this.business.value().slug);

  constructor() {
    // Carga los datos en el formulario cuando llegan de la API.
    effect(() => {
      if (!this.business.hasValue()) return;
      const b = this.business.value();
      this.form.reset({ name: b.name, slug: b.slug, timeZone: b.timeZone, whatsApp: b.whatsApp ?? '', accentColor: b.accentColor });
    });

    this.form.valueChanges.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe(() => {
      if (Object.keys(this.serverErrors()).length > 0) this.serverErrors.set({});
    });
  }

  protected invalid(control: Field): boolean {
    const c = this.form.controls[control];
    return (c.invalid && c.touched) || !!this.serverErrors()[control];
  }

  protected slugFormatError(): boolean {
    const slug = this.values().slug ?? '';
    return slug.length > 0 && !hasValidSlugFormat(slug);
  }

  protected pickColor(color: string): void {
    this.form.controls.accentColor.setValue(color);
    this.form.controls.accentColor.markAsDirty();
  }

  protected onSlugInput(): void {
    const control = this.form.controls.slug;
    const cleaned = control.value.toLowerCase().replace(/\s+/g, '-');
    if (cleaned !== control.value) control.setValue(cleaned);
  }

  protected save(): void {
    if (this.form.invalid || this.slugFormatError()) {
      this.form.markAllAsTouched();
      return;
    }
    const raw = this.form.getRawValue();
    const body: UpdateBusiness = { ...raw, name: raw.name.trim(), whatsApp: raw.whatsApp.trim() || null };

    this.saving.set(true);
    this.error.set(null);
    this.http.put<BusinessSettings>(`${API_BASE}/business`, body, { context: silentErrors() }).subscribe({
      next: (saved) => {
        this.business.set(saved);
        this.auth.updateTenant({ id: saved.id, name: saved.name, slug: saved.slug });
        this.toasts.success('Cambios guardados.');
        this.saving.set(false);
      },
      error: (e: unknown) => {
        const fields = getFieldErrors(e);
        this.serverErrors.set(fields);
        if (Object.keys(fields).length === 0) this.error.set(getErrorMessage(e));
        this.saving.set(false);
      },
    });
  }
}
