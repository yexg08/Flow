import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { readableTextOn } from '../../shared/format';
import { formatTime, toLocalIso } from '../../shared/schedule';
import { PublicSlot } from './public.models';

export interface DayOption {
  /** "yyyy-MM-dd" */
  date: string;
  top: string;
  day: number;
  month: string;
}

/** Los próximos `count` días a partir de hoy, listos para mostrar. */
export function nextDays(count: number, from = new Date()): DayOption[] {
  const weekday = new Intl.DateTimeFormat('es-CO', { weekday: 'short' });
  const month = new Intl.DateTimeFormat('es-CO', { month: 'short' });
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(from.getFullYear(), from.getMonth(), from.getDate() + i);
    const top = i === 0 ? 'Hoy' : i === 1 ? 'Mañana' : weekday.format(d).replace('.', '');
    return { date: toLocalIso(d).slice(0, 10), top, day: d.getDate(), month: month.format(d).replace('.', '') };
  });
}

/** Tira horizontal de días para elegir fecha. */
@Component({
  selector: 'app-day-strip',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="-mx-1 flex gap-2 overflow-x-auto px-1 pb-2" role="radiogroup" [attr.aria-label]="label()">
      @for (day of days(); track day.date) {
        @let selected = day.date === value();
        <button
          type="button"
          role="radio"
          [attr.aria-checked]="selected"
          [attr.aria-label]="day.top + ' ' + day.day + ' de ' + day.month"
          class="flex w-16 shrink-0 cursor-pointer flex-col items-center rounded-xl border py-2.5 transition"
          [class]="selected ? 'border-transparent shadow-lg' : 'border-line bg-surface hover:bg-surface-2'"
          [style.background-color]="selected ? accent() : null"
          [style.color]="selected ? textOnAccent() : null"
          (click)="pick.emit(day.date)"
        >
          <span class="text-[11px] font-semibold capitalize" [class.opacity-80]="selected" [class.text-muted]="!selected">{{ day.top }}</span>
          <span class="text-xl font-extrabold">{{ day.day }}</span>
          <span class="text-[11px] capitalize" [class.opacity-80]="selected" [class.text-muted]="!selected">{{ day.month }}</span>
        </button>
      }
    </div>
  `,
})
export class DayStrip {
  readonly days = input.required<DayOption[]>();
  readonly value = input<string | null>(null);
  readonly accent = input('#7c3aed');
  readonly label = input('Elige el día');
  readonly pick = output<string>();

  protected readonly textOnAccent = computed(() => readableTextOn(this.accent()));
}

/** Grilla de horarios libres. */
@Component({
  selector: 'app-slot-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <div class="grid grid-cols-3 gap-2 sm:grid-cols-4" aria-label="Buscando horarios">
        @for (i of [1, 2, 3, 4, 5, 6, 7, 8]; track i) {
          <span class="h-11 animate-pulse rounded-xl bg-surface-2"></span>
        }
      </div>
    } @else if (slots().length === 0) {
      <p class="rounded-xl border border-dashed border-line px-4 py-6 text-center text-sm text-muted">{{ emptyText() }}</p>
    } @else {
      <div class="grid grid-cols-3 gap-2 sm:grid-cols-4" role="radiogroup" aria-label="Elige la hora">
        @for (slot of slots(); track slot.time) {
          @let selected = slot.time === value();
          <button
            type="button"
            role="radio"
            [attr.aria-checked]="selected"
            class="h-11 cursor-pointer rounded-xl border text-sm font-semibold tabular-nums transition"
            [class]="selected ? 'border-transparent shadow-lg' : 'border-line bg-surface hover:bg-surface-2'"
            [style.background-color]="selected ? accent() : null"
            [style.color]="selected ? textOnAccent() : null"
            (click)="pick.emit(slot.time)"
          >
            {{ format(slot.time) }}
          </button>
        }
      </div>
    }
  `,
})
export class SlotGrid {
  readonly slots = input.required<PublicSlot[]>();
  readonly value = input<string | null>(null);
  readonly loading = input(false);
  readonly accent = input('#7c3aed');
  readonly emptyText = input('No hay horarios libres este día. Prueba con otro.');
  readonly pick = output<string>();

  protected readonly textOnAccent = computed(() => readableTextOn(this.accent()));
  protected readonly format = formatTime;
}
