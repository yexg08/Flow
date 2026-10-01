import { addDays, layoutLanes, minuteOfDay, startOfWeek } from './agenda-layout';

const r = (item: { start: number; end: number }) => item;

describe('layoutLanes', () => {
  it('pone en una sola columna lo que no se cruza', () => {
    const placed = layoutLanes([{ start: 540, end: 600 }, { start: 600, end: 660 }], r);
    expect(placed.map((p) => [p.lane, p.lanes])).toEqual([
      [0, 1],
      [0, 1],
    ]);
  });

  it('reparte lado a lado lo que se cruza y reutiliza columnas libres', () => {
    // A 9:00–10:00, B 9:30–10:30 (se cruza con A), C 10:00–11:00 (cabe donde estaba A)
    const placed = layoutLanes(
      [
        { id: 'A', start: 540, end: 600 },
        { id: 'B', start: 570, end: 630 },
        { id: 'C', start: 600, end: 660 },
      ],
      r,
    );
    const byId = Object.fromEntries(placed.map((p) => [p.item.id, [p.lane, p.lanes]]));
    expect(byId).toEqual({ A: [0, 2], B: [1, 2], C: [0, 2] });
  });

  it('cada grupo separado tiene su propio número de columnas', () => {
    const placed = layoutLanes(
      [
        { id: 'A', start: 540, end: 600 },
        { id: 'B', start: 540, end: 600 },
        { id: 'C', start: 720, end: 780 },
      ],
      r,
    );
    expect(placed.find((p) => p.item.id === 'C')).toMatchObject({ lane: 0, lanes: 1 });
    expect(placed.find((p) => p.item.id === 'B')).toMatchObject({ lanes: 2 });
  });
});

describe('fechas', () => {
  it('minuteOfDay lee la hora local sin convertir zonas', () => {
    expect(minuteOfDay('2099-01-05T09:30:00')).toBe(570);
  });

  it('startOfWeek va al lunes, también cruzando de mes', () => {
    expect(startOfWeek('2099-03-01')).toBe('2099-02-23'); // domingo 1 de marzo → lunes 23 de febrero
    expect(startOfWeek('2099-02-23')).toBe('2099-02-23');
  });

  it('addDays cruza meses y años', () => {
    expect(addDays('2099-12-31', 1)).toBe('2100-01-01');
    expect(addDays('2099-03-01', -1)).toBe('2099-02-28');
  });
});
