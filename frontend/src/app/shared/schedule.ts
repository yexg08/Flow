export type Weekday = 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | 'Sunday';

export interface TimeRange {
  dayOfWeek: Weekday;
  /** "HH:mm", hora local del negocio. */
  start: string;
  end: string;
}

export interface Slot {
  start: string;
  end: string;
}

export const WEEKDAYS: { id: Weekday; label: string; short: string }[] = [
  { id: 'Monday', label: 'Lunes', short: 'Lun' },
  { id: 'Tuesday', label: 'Martes', short: 'Mar' },
  { id: 'Wednesday', label: 'Miércoles', short: 'Mié' },
  { id: 'Thursday', label: 'Jueves', short: 'Jue' },
  { id: 'Friday', label: 'Viernes', short: 'Vie' },
  { id: 'Saturday', label: 'Sábado', short: 'Sáb' },
  { id: 'Sunday', label: 'Domingo', short: 'Dom' },
];

export const MAX_RANGES_PER_DAY = 6;

/** Horario de arranque para quien no quiere armarlo desde cero: L–V 8–12 y 14–18, sábado 8–12. */
export const TYPICAL_WEEK: Record<Weekday, Slot[]> = {
  Monday: [{ start: '08:00', end: '12:00' }, { start: '14:00', end: '18:00' }],
  Tuesday: [{ start: '08:00', end: '12:00' }, { start: '14:00', end: '18:00' }],
  Wednesday: [{ start: '08:00', end: '12:00' }, { start: '14:00', end: '18:00' }],
  Thursday: [{ start: '08:00', end: '12:00' }, { start: '14:00', end: '18:00' }],
  Friday: [{ start: '08:00', end: '12:00' }, { start: '14:00', end: '18:00' }],
  Saturday: [{ start: '08:00', end: '12:00' }],
  Sunday: [],
};

const TIME = /^([01]\d|2[0-3]):([0-5]\d)$/;

const minutes = (time: string): number => Number(time.slice(0, 2)) * 60 + Number(time.slice(3, 5));

/** Mismas reglas que Schedule.cs en el backend. Devuelve el problema del día o null si está bien. */
export function validateDay(slots: Slot[]): string | null {
  if (slots.length > MAX_RANGES_PER_DAY) return `Máximo ${MAX_RANGES_PER_DAY} tramos por día.`;
  for (const slot of slots) {
    if (!TIME.test(slot.start) || !TIME.test(slot.end)) return 'Completa las horas.';
    if (minutes(slot.start) % 5 !== 0 || minutes(slot.end) % 5 !== 0) return 'Las horas deben ir de 5 en 5 minutos.';
    if (minutes(slot.end) <= minutes(slot.start)) return 'El cierre debe ser después de la apertura.';
  }
  const ordered = [...slots].sort((a, b) => minutes(a.start) - minutes(b.start));
  for (let i = 1; i < ordered.length; i++) {
    if (minutes(ordered[i].start) < minutes(ordered[i - 1].end)) return 'Hay tramos que se cruzan.';
  }
  return null;
}

/** "08:00" → "8:00". */
export function formatTime(time: string): string {
  return time.startsWith('0') ? time.slice(1) : time;
}

/** Agrupa el horario por día, en orden de lunes a domingo. */
export function groupByDay(ranges: TimeRange[]): Record<Weekday, Slot[]> {
  const week = Object.fromEntries(WEEKDAYS.map((d) => [d.id, [] as Slot[]])) as Record<Weekday, Slot[]>;
  for (const r of ranges) week[r.dayOfWeek].push({ start: r.start, end: r.end });
  for (const day of WEEKDAYS) week[day.id].sort((a, b) => minutes(a.start) - minutes(b.start));
  return week;
}

/**
 * Resumen legible del horario, uniendo días seguidos con el mismo horario:
 * "Lun–Vie 8:00–12:00, 14:00–18:00 · Sáb 8:00–12:00". Vacío si no hay horario.
 */
export function summarizeWeek(ranges: TimeRange[]): string {
  const week = groupByDay(ranges);
  const key = (slots: Slot[]) => slots.map((s) => `${formatTime(s.start)}–${formatTime(s.end)}`).join(', ');

  const parts: string[] = [];
  let i = 0;
  while (i < WEEKDAYS.length) {
    const hours = key(week[WEEKDAYS[i].id]);
    let j = i;
    while (j + 1 < WEEKDAYS.length && key(week[WEEKDAYS[j + 1].id]) === hours) j++;
    if (hours) {
      const days = i === j ? WEEKDAYS[i].short : `${WEEKDAYS[i].short}–${WEEKDAYS[j].short}`;
      parts.push(`${days} ${hours}`);
    }
    i = j + 1;
  }
  return parts.join(' · ');
}

/** Lee "2026-10-05T08:00:00" como hora de reloj (sin convertir zonas: ya es la hora local del negocio). */
export function parseLocal(value: string): Date {
  const [date, time = '00:00'] = value.split('T');
  const [y, m, d] = date.split('-').map(Number);
  const [h, min] = time.split(':').map(Number);
  return new Date(y, m - 1, d, h, min);
}

/** Fecha y hora local en el formato que espera la API: "2026-10-05T08:00". */
export function toLocalIso(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

const dayFormat = new Intl.DateTimeFormat('es-CO', { weekday: 'short', day: 'numeric', month: 'short' });
const dayYearFormat = new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', year: 'numeric' });
const timeFormat = (d: Date) => `${d.getHours()}:${String(d.getMinutes()).padStart(2, '0')}`;

/**
 * Describe un bloqueo en palabras:
 * - Días completos (de 00:00 a 00:00): "25 dic 2099" o "2 mar 2099 – 6 mar 2099".
 * - Mismo día: "lun, 5 oct · 8:00–12:00".
 * - Varios días con horas: "lun, 5 oct 8:00 – mié, 7 oct 18:00".
 */
export function describeRange(startsAt: string, endsAt: string): string {
  const start = parseLocal(startsAt);
  const end = parseLocal(endsAt);
  const midnight = (d: Date) => d.getHours() === 0 && d.getMinutes() === 0;

  if (midnight(start) && midnight(end)) {
    const lastDay = new Date(end.getFullYear(), end.getMonth(), end.getDate() - 1);
    return lastDay.getTime() <= start.getTime()
      ? dayYearFormat.format(start)
      : `${dayYearFormat.format(start)} – ${dayYearFormat.format(lastDay)}`;
  }

  const sameDay = start.toDateString() === end.toDateString();
  return sameDay
    ? `${dayFormat.format(start)} · ${timeFormat(start)}–${timeFormat(end)}`
    : `${dayFormat.format(start)} ${timeFormat(start)} – ${dayFormat.format(end)} ${timeFormat(end)}`;
}
