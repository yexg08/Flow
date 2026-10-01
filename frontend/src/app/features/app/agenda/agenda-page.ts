import { Dialog } from '@angular/cdk/dialog';
import { HttpParams, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroCheckCircle, heroChevronLeft, heroChevronRight, heroNoSymbol, heroPlus } from '@ng-icons/heroicons/outline';
import { API_BASE } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { addDays, layoutLanes, minuteOfDay, startOfWeek } from '../../../shared/agenda-layout';
import { DIALOG_DEFAULTS } from '../../../shared/confirm';
import { formatTime, parseLocal, toLocalIso, Weekday, WEEKDAYS } from '../../../shared/schedule';
import { Service, StaffMember } from '../business.models';
import { Agenda, AgendaAppointment } from './agenda.models';
import { AppointmentDialog, AppointmentDialogData } from './appointment-dialog';
import { NewAppointmentData, NewAppointmentDialog } from './new-appointment-dialog';

type View = 'day' | 'week';

/** Alto de un minuto en la grilla: 1.2 px → una hora mide 72 px. */
const PX_PER_MINUTE = 1.2;
const SNAP_MINUTES = 15;
const VIEW_KEY = 'flow-agenda-view';

interface Column {
  key: string;
  date: string;
  staffId: string | null;
  title: string;
  subtitle: string;
  color: string | null;
  isToday: boolean;
}

interface Block {
  appointment: AgendaAppointment;
  top: number;
  height: number;
  left: number;
  width: number;
  compact: boolean;
}

interface Shade {
  key: string;
  top: number;
  height: number;
  label: string;
}

const JS_DAY_TO_WEEKDAY: Weekday[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
const weekdayOf = (date: string): Weekday => JS_DAY_TO_WEEKDAY[parseLocal(`${date}T00:00`).getDay()];
const hhmmToMinutes = (time: string): number => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));

@Component({
  selector: 'app-agenda-page',
  imports: [NgIcon],
  providers: [provideIcons({ heroCheckCircle, heroChevronLeft, heroChevronRight, heroNoSymbol, heroPlus })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './agenda-page.html',
})
export class AgendaPage {
  private readonly dialog = inject(Dialog);
  private readonly toasts = inject(ToastService);

  protected readonly view = signal<View>(this.storedView());
  protected readonly date = signal(toLocalIso(new Date()).slice(0, 10));
  /** Vista semanal: ver a una sola persona (null = todo el equipo). */
  protected readonly staffFilter = signal<string | null>(null);
  protected readonly showCancelled = signal(false);
  private readonly now = signal(new Date());

  protected readonly range = computed(() => {
    const from = this.view() === 'day' ? this.date() : startOfWeek(this.date());
    return { from, to: this.view() === 'day' ? from : addDays(from, 6) };
  });

  protected readonly agenda = httpResource<Agenda>(() => ({
    url: `${API_BASE}/appointments/agenda`,
    params: new HttpParams().set('from', this.range().from).set('to', this.range().to),
  }));
  private readonly staff = httpResource<StaffMember[]>(() => `${API_BASE}/staff`, { defaultValue: [] });
  private readonly services = httpResource<Service[]>(() => `${API_BASE}/services`, { defaultValue: [] });

  protected readonly staffList = computed(() => this.staff.value());
  protected readonly pxPerMinute = PX_PER_MINUTE;

  private readonly appointments = computed(() => {
    const all = this.agenda.hasValue() ? this.agenda.value().appointments : [];
    const filter = this.view() === 'week' ? this.staffFilter() : null;
    return all.filter((a) => (this.showCancelled() || a.status !== 'Cancelled') && (!filter || a.staffMemberId === filter));
  });

  protected readonly counts = computed(() => {
    const all = this.agenda.hasValue() ? this.agenda.value().appointments : [];
    return { active: all.filter((a) => a.status !== 'Cancelled').length, cancelled: all.filter((a) => a.status === 'Cancelled').length };
  });

  /** Horas visibles: las del horario del equipo en esos días y las de las citas, con 8:00–18:00 como mínimo razonable. */
  protected readonly hours = computed(() => {
    const days = this.view() === 'day' ? [weekdayOf(this.date())] : WEEKDAYS.map((d) => d.id);
    const minutes: number[] = [];
    for (const person of this.staffList()) {
      for (const range of person.workingHours.filter((w) => days.includes(w.dayOfWeek))) {
        minutes.push(hhmmToMinutes(range.start), hhmmToMinutes(range.end));
      }
    }
    for (const a of this.appointments()) minutes.push(minuteOfDay(a.startsAt), minuteOfDay(a.endsAt) || 24 * 60);
    const first = minutes.length ? Math.min(...minutes) : 8 * 60;
    const last = minutes.length ? Math.max(...minutes) : 18 * 60;
    const start = Math.max(0, Math.floor(Math.min(first, 8 * 60) / 60));
    const end = Math.min(24, Math.ceil(Math.max(last, 18 * 60) / 60));
    return { start, end, labels: Array.from({ length: end - start }, (_, i) => `${start + i}:00`) };
  });

  protected readonly bodyHeight = computed(() => (this.hours().end - this.hours().start) * 60 * PX_PER_MINUTE);

  protected readonly columns = computed<Column[]>(() => {
    const today = toLocalIso(this.now()).slice(0, 10);
    if (this.view() === 'week') {
      const label = new Intl.DateTimeFormat('es-CO', { weekday: 'short' });
      return Array.from({ length: 7 }, (_, i) => {
        const date = addDays(this.range().from, i);
        const d = parseLocal(`${date}T00:00`);
        return { key: date, date, staffId: this.staffFilter(), title: label.format(d).replace('.', ''), subtitle: String(d.getDate()), color: null, isToday: date === today };
      });
    }
    // Vista de día: una columna por persona activa, más quien tenga citas ese día aunque esté inactiva.
    const withAppointments = new Set(this.appointments().map((a) => a.staffMemberId));
    return this.staffList()
      .filter((s) => s.isActive || withAppointments.has(s.id))
      .map((s) => ({
        key: s.id,
        date: this.date(),
        staffId: s.id,
        title: s.name,
        subtitle: s.isActive ? '' : 'No recibe reservas',
        color: s.color,
        isToday: this.date() === today,
      }));
  });

  protected readonly gridTemplate = computed(() => `3.5rem repeat(${Math.max(this.columns().length, 1)}, minmax(${this.view() === 'day' ? 10 : 8}rem, 1fr))`);

  protected readonly blocks = computed(() => {
    const startMinute = this.hours().start * 60;
    const result = new Map<string, Block[]>();
    for (const column of this.columns()) {
      const items = this.appointments().filter(
        (a) => a.startsAt.startsWith(column.date) && (this.view() === 'week' || a.staffMemberId === column.staffId),
      );
      const placed = layoutLanes(items, (a) => ({ start: minuteOfDay(a.startsAt), end: minuteOfDay(a.endsAt) || 24 * 60 }));
      result.set(
        column.key,
        placed.map(({ item, lane, lanes }) => {
          const height = Math.max(((minuteOfDay(item.endsAt) || 24 * 60) - minuteOfDay(item.startsAt)) * PX_PER_MINUTE - 2, 20);
          return {
            appointment: item,
            top: (minuteOfDay(item.startsAt) - startMinute) * PX_PER_MINUTE + 1,
            height,
            left: (lane / lanes) * 100,
            width: 100 / lanes,
            compact: height < 44,
          };
        }),
      );
    }
    return result;
  });

  /** Bloqueos sombreados: los de todo el negocio y los de la persona de la columna (o la filtrada en la semana). */
  protected readonly shades = computed(() => {
    const blocks = this.agenda.hasValue() ? this.agenda.value().timeOff : [];
    const { start, end } = this.hours();
    const result = new Map<string, Shade[]>();
    for (const column of this.columns()) {
      const dayStart = parseLocal(`${column.date}T00:00`).getTime();
      const shades: Shade[] = [];
      for (const block of blocks) {
        const applies = block.staffMemberId === null || block.staffMemberId === column.staffId;
        if (!applies) continue;
        const from = Math.max((parseLocal(block.startsAt).getTime() - dayStart) / 60_000, start * 60);
        const to = Math.min((parseLocal(block.endsAt).getTime() - dayStart) / 60_000, end * 60);
        if (to <= from) continue;
        shades.push({
          key: block.id,
          top: (from - start * 60) * PX_PER_MINUTE,
          height: (to - from) * PX_PER_MINUTE,
          label: block.reason ?? (block.staffName ? 'Bloqueado' : 'Negocio cerrado'),
        });
      }
      result.set(column.key, shades);
    }
    return result;
  });

  protected readonly nowTop = computed(() => {
    const now = this.now();
    const minutes = now.getHours() * 60 + now.getMinutes();
    const { start, end } = this.hours();
    return minutes >= start * 60 && minutes <= end * 60 ? (minutes - start * 60) * PX_PER_MINUTE : null;
  });

  protected readonly heading = computed(() => {
    const { from, to } = this.range();
    if (this.view() === 'day') {
      return new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }).format(parseLocal(`${from}T00:00`));
    }
    const short = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short' });
    return `${short.format(parseLocal(`${from}T00:00`))} – ${short.format(parseLocal(`${to}T00:00`))} ${to.slice(0, 4)}`;
  });

  protected readonly formatTime = formatTime;

  constructor() {
    const timer = setInterval(() => this.now.set(new Date()), 60_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  protected setView(view: View): void {
    this.view.set(view);
    try {
      localStorage.setItem(VIEW_KEY, view);
    } catch {
      // Sin almacenamiento disponible: la vista funciona igual, solo no se recuerda.
    }
  }

  protected shift(direction: -1 | 1): void {
    this.date.update((d) => addDays(d, direction * (this.view() === 'day' ? 1 : 7)));
  }

  protected goToday(): void {
    this.date.set(toLocalIso(new Date()).slice(0, 10));
  }

  /** En la semana, un clic en el encabezado de un día abre ese día. */
  protected openDay(date: string): void {
    this.date.set(date);
    this.setView('day');
  }

  protected time(localIso: string): string {
    return formatTime(localIso.slice(11, 16));
  }

  /** Clic en un hueco de la grilla: nueva cita a esa hora (redondeada a 15 minutos) y con esa persona. */
  protected onGridClick(event: MouseEvent, column: Column): void {
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const minute = this.hours().start * 60 + (event.clientY - rect.top) / PX_PER_MINUTE;
    const snapped = Math.floor(minute / SNAP_MINUTES) * SNAP_MINUTES;
    const time = `${String(Math.floor(snapped / 60)).padStart(2, '0')}:${String(snapped % 60).padStart(2, '0')}`;
    this.newAppointment(column.date, time, column.staffId);
  }

  protected newAppointment(date = this.date(), time = '09:00', staffId: string | null = this.view() === 'week' ? this.staffFilter() : null): void {
    const data: NewAppointmentData = { services: this.services.value(), staff: this.staffList(), date, time, staffId };
    this.dialog
      .open<AgendaAppointment>(NewAppointmentDialog, { data, ...DIALOG_DEFAULTS })
      .closed.subscribe((created) => {
        if (!created) return;
        this.toasts.success(`Cita agendada para ${created.customerName}.`);
        this.agenda.reload();
      });
  }

  protected open(appointment: AgendaAppointment, event: Event): void {
    event.stopPropagation();
    const data: AppointmentDialogData = { appointment, staff: this.staffList() };
    this.dialog.open(AppointmentDialog, { data, ...DIALOG_DEFAULTS }).closed.subscribe(() => this.agenda.reload());
  }

  protected blockStyle(color: string): string {
    return `background-color: color-mix(in srgb, ${color} 20%, var(--color-surface)); border-left-color: ${color};`;
  }

  private storedView(): View {
    try {
      return localStorage.getItem(VIEW_KEY) === 'week' ? 'week' : 'day';
    } catch {
      return 'day';
    }
  }
}
