import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroArrowLeft, heroDocumentDuplicate, heroPlus, heroSparkles, heroXMark } from '@ng-icons/heroicons/outline';
import { AuthService } from '../../../core/auth/auth.service';
import { API_BASE, getErrorMessage, silentErrors } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import {
  MAX_RANGES_PER_DAY,
  Slot,
  TimeRange,
  TYPICAL_WEEK,
  WEEKDAYS,
  Weekday,
  groupByDay,
  validateDay,
} from '../../../shared/schedule';
import { StaffMember } from '../business.models';

type Week = Record<Weekday, Slot[]>;

const WORKDAYS: Weekday[] = ['Tuesday', 'Wednesday', 'Thursday', 'Friday'];

/** Horario semanal de una persona: varios tramos por día (mañana y tarde, por ejemplo). */
@Component({
  selector: 'app-schedule-page',
  imports: [RouterLink, NgIcon],
  providers: [provideIcons({ heroArrowLeft, heroDocumentDuplicate, heroPlus, heroSparkles, heroXMark })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './schedule-page.html',
})
export class SchedulePage {
  private readonly http = inject(HttpClient);
  private readonly toasts = inject(ToastService);
  protected readonly auth = inject(AuthService);

  /** Viene de la ruta equipo/:id/horario. */
  readonly id = input.required<string>();

  protected readonly staff = httpResource<StaffMember>(() => `${API_BASE}/staff/${this.id()}`);
  protected readonly weekdays = WEEKDAYS;
  protected readonly maxRanges = MAX_RANGES_PER_DAY;

  protected readonly week = signal<Week>(groupByDay([]));
  private readonly saved = signal<string>('');
  protected readonly saving = signal(false);

  protected readonly errors = computed(() => {
    const week = this.week();
    return Object.fromEntries(WEEKDAYS.map((d) => [d.id, validateDay(week[d.id])])) as Record<Weekday, string | null>;
  });
  protected readonly hasErrors = computed(() => Object.values(this.errors()).some((e) => e !== null));
  protected readonly dirty = computed(() => JSON.stringify(this.week()) !== this.saved());
  protected readonly editable = computed(() => this.auth.isOwner());

  constructor() {
    effect(() => {
      if (!this.staff.hasValue()) return;
      const week = groupByDay(this.staff.value().workingHours);
      this.week.set(week);
      this.saved.set(JSON.stringify(week));
    });
  }

  protected setOpen(day: Weekday, open: boolean): void {
    this.update(day, () => (open ? [{ start: '08:00', end: '17:00' }] : []));
  }

  protected addSlot(day: Weekday): void {
    this.update(day, (slots) => {
      const last = slots.at(-1);
      if (!last) return [{ start: '08:00', end: '17:00' }];
      // El tramo nuevo arranca una hora después del último, para no cruzarse.
      const startHour = Math.min(Number(last.end.slice(0, 2)) + 1, 22);
      const pad = (n: number) => String(n).padStart(2, '0');
      return [...slots, { start: `${pad(startHour)}:${last.end.slice(3)}`, end: `${pad(Math.min(startHour + 2, 23))}:${last.end.slice(3)}` }];
    });
  }

  protected removeSlot(day: Weekday, index: number): void {
    this.update(day, (slots) => slots.filter((_, i) => i !== index));
  }

  protected setTime(day: Weekday, index: number, field: 'start' | 'end', value: string): void {
    this.update(day, (slots) => slots.map((s, i) => (i === index ? { ...s, [field]: value } : s)));
  }

  protected useTypical(): void {
    this.week.set(structuredClone(TYPICAL_WEEK));
  }

  protected copyMondayToWorkdays(): void {
    const monday = this.week().Monday;
    this.week.update((w) => ({ ...w, ...Object.fromEntries(WORKDAYS.map((d) => [d, monday.map((s) => ({ ...s }))])) }));
  }

  protected save(): void {
    if (this.hasErrors()) return;
    const week = this.week();
    const ranges: TimeRange[] = WEEKDAYS.flatMap((d) => week[d.id].map((s) => ({ dayOfWeek: d.id, start: s.start, end: s.end })));

    this.saving.set(true);
    this.http.put<StaffMember>(`${API_BASE}/staff/${this.id()}/schedule`, { ranges }, { context: silentErrors() }).subscribe({
      next: (saved) => {
        this.staff.set(saved);
        this.toasts.success('Horario guardado.');
        this.saving.set(false);
      },
      error: (e: unknown) => {
        this.toasts.error(getErrorMessage(e));
        this.saving.set(false);
      },
    });
  }

  private update(day: Weekday, change: (slots: Slot[]) => Slot[]): void {
    this.week.update((w) => ({ ...w, [day]: change(w[day]) }));
  }
}
