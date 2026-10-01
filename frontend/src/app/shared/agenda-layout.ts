/** Minutos desde la medianoche de una fecha local "2026-10-05T09:30:00" → 570. */
export function minuteOfDay(localIso: string): number {
  return Number(localIso.slice(11, 13)) * 60 + Number(localIso.slice(14, 16));
}

/** "2026-10-05" + n días. */
export function addDays(date: string, days: number): string {
  const [y, m, d] = date.split('-').map(Number);
  const result = new Date(y, m - 1, d + days);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${result.getFullYear()}-${pad(result.getMonth() + 1)}-${pad(result.getDate())}`;
}

/** Lunes de la semana de esa fecha. */
export function startOfWeek(date: string): string {
  const [y, m, d] = date.split('-').map(Number);
  const weekday = (new Date(y, m - 1, d).getDay() + 6) % 7; // lunes = 0
  return addDays(date, -weekday);
}

export interface TimedItem {
  start: number;
  end: number;
}

export interface Placed<T> {
  item: T;
  /** Columna dentro del grupo de elementos que se cruzan (0, 1, 2...). */
  lane: number;
  /** Cuántas columnas tiene ese grupo: el ancho de cada elemento es 1/lanes. */
  lanes: number;
}

/**
 * Reparte elementos que se cruzan en el tiempo en columnas, para dibujarlos lado a lado (como en Google Calendar):
 * cada grupo de elementos encadenados por cruces comparte el mismo número de columnas, y cada elemento va en la
 * primera columna libre.
 */
export function layoutLanes<T>(items: T[], range: (item: T) => TimedItem): Placed<T>[] {
  const sorted = [...items].sort((a, b) => range(a).start - range(b).start || range(b).end - range(a).end);
  const result: Placed<T>[] = [];
  let group: Placed<T>[] = [];
  let laneEnds: number[] = [];
  let groupEnd = -Infinity;

  const closeGroup = () => {
    for (const placed of group) placed.lanes = laneEnds.length;
    result.push(...group);
    group = [];
    laneEnds = [];
  };

  for (const item of sorted) {
    const { start, end } = range(item);
    if (start >= groupEnd) {
      closeGroup();
      groupEnd = -Infinity;
    }
    let lane = laneEnds.findIndex((laneEnd) => laneEnd <= start);
    if (lane === -1) {
      lane = laneEnds.length;
      laneEnds.push(end);
    } else {
      laneEnds[lane] = end;
    }
    group.push({ item, lane, lanes: 0 });
    groupEnd = Math.max(groupEnd, end);
  }
  closeGroup();
  return result;
}
