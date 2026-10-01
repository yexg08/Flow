import { HttpClient, HttpErrorResponse, HttpParams, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroArrowLeft, heroCheck, heroClock } from '@ng-icons/heroicons/outline';
import { API_BASE, getErrorMessage, getFieldErrors, silentErrors } from '../../core/http/api';
import { formatDuration, formatPrice, readableTextOn } from '../../shared/format';
import { formatTime, parseLocal } from '../../shared/schedule';
import { ThemeToggle } from '../../shared/theme-toggle';
import { NotFound } from '../not-found';
import { DayStrip, SlotGrid, nextDays } from './booking-widgets';
import { BookingResult, PublicAvailability, PublicBusinessPage } from './public.models';

/** Reservar un servicio desde la página pública: persona → día → hora → datos. */
@Component({
  selector: 'app-book-appointment',
  imports: [ReactiveFormsModule, RouterLink, NgIcon, ThemeToggle, NotFound, DayStrip, SlotGrid],
  providers: [provideIcons({ heroArrowLeft, heroCheck, heroClock })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './book-appointment.html',
})
export class BookAppointment {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /** Vienen de la ruta n/:slug/reservar/:serviceId. */
  readonly slug = input.required<string>();
  readonly serviceId = input.required<string>();

  protected readonly page = httpResource<PublicBusinessPage>(() => `${API_BASE}/public/businesses/${encodeURIComponent(this.slug())}`);
  protected readonly service = computed(() =>
    this.page.hasValue() ? (this.page.value().services.find((s) => s.id === this.serviceId()) ?? null) : null,
  );
  protected readonly staffOptions = computed(() =>
    this.page.hasValue() ? this.page.value().staff.filter((s) => s.serviceIds.includes(this.serviceId())) : [],
  );
  protected readonly accent = computed(() => (this.page.hasValue() ? this.page.value().accentColor : '#7c3aed'));
  protected readonly accentText = computed(() => readableTextOn(this.accent()));

  protected readonly days = nextDays(14);
  /** Null = cualquier persona. */
  protected readonly staffId = signal<string | null>(null);
  protected readonly date = signal(this.days[0].date);
  protected readonly time = signal<string | null>(null);

  protected readonly availability = httpResource<PublicAvailability>(() => {
    if (!this.service()) return undefined;
    let params = new HttpParams().set('serviceId', this.serviceId()).set('date', this.date());
    const staff = this.staffId();
    if (staff) params = params.set('staffId', staff);
    return { url: `${API_BASE}/public/businesses/${encodeURIComponent(this.slug())}/availability`, params };
  });
  protected readonly slots = computed(() => (this.availability.hasValue() ? this.availability.value().slots : []));

  protected readonly form = inject(NonNullableFormBuilder).group({
    customerName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]],
    customerPhone: ['', [Validators.required, Validators.pattern(/^[+\d\s().-]{7,20}$/)]],
    customerEmail: ['', [Validators.email, Validators.maxLength(256)]],
    note: ['', [Validators.maxLength(300)]],
    acceptsPrivacy: [false, [Validators.requiredTrue]],
  });
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string>>({});

  protected readonly summary = computed(() => {
    const time = this.time();
    if (!time) return null;
    const when = parseLocal(`${this.date()}T${time}`);
    const day = new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long' }).format(when);
    const staff = this.staffOptions().find((s) => s.id === this.staffId());
    return { day, time: formatTime(time), staff: staff?.name ?? 'Cualquier persona disponible' };
  });

  protected readonly formatPrice = formatPrice;
  protected readonly formatDuration = formatDuration;

  constructor() {
    const title = inject(Title);
    effect(() => {
      if (this.page.hasValue()) title.setTitle(`Reservar · ${this.page.value().name}`);
    });
  }

  protected pickStaff(id: string | null): void {
    this.staffId.set(id);
    this.time.set(null);
  }

  protected pickDate(date: string): void {
    this.date.set(date);
    this.time.set(null);
  }

  protected invalid(field: 'customerName' | 'customerPhone' | 'customerEmail' | 'acceptsPrivacy'): boolean {
    const c = this.form.controls[field];
    return (c.invalid && c.touched) || !!this.serverErrors()[field];
  }

  protected submit(): void {
    const time = this.time();
    if (!time || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      serviceId: this.serviceId(),
      staffMemberId: this.staffId(),
      date: this.date(),
      time,
      customerName: v.customerName.trim(),
      customerPhone: v.customerPhone.trim(),
      customerEmail: v.customerEmail.trim() || null,
      note: v.note.trim() || null,
      acceptsPrivacy: v.acceptsPrivacy,
    };

    this.saving.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    this.http
      .post<BookingResult>(`${API_BASE}/public/businesses/${encodeURIComponent(this.slug())}/appointments`, body, {
        context: silentErrors(),
      })
      .subscribe({
        next: (result) => void this.router.navigate(['/cita', result.manageToken], { queryParams: { nueva: 1 } }),
        error: (e: unknown) => {
          this.saving.set(false);
          if (e instanceof HttpErrorResponse && e.status === 409) {
            // Otra persona tomó ese horario mientras llenaba los datos: se muestran los que quedan.
            this.time.set(null);
            this.availability.reload();
          }
          const fields = getFieldErrors(e);
          this.serverErrors.set(fields);
          if (Object.keys(fields).length === 0) this.error.set(getErrorMessage(e));
        },
      });
  }
}
