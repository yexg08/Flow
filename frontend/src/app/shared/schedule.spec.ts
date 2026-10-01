import { buildTimeOffRange } from '../features/app/time-off/time-off-dialog';
import { TimeRange, describeRange, summarizeWeek, toLocalIso, validateDay } from './schedule';

describe('validateDay (mismas reglas que Schedule.cs en el backend)', () => {
  it('acepta mañana y tarde, y tramos que se tocan', () => {
    expect(validateDay([{ start: '08:00', end: '12:00' }, { start: '14:00', end: '18:00' }])).toBeNull();
    expect(validateDay([{ start: '08:00', end: '12:00' }, { start: '12:00', end: '13:00' }])).toBeNull();
  });

  it.each([
    [[{ start: '08:00', end: '12:00' }, { start: '11:00', end: '14:00' }], 'Hay tramos que se cruzan.'],
    [[{ start: '12:00', end: '08:00' }], 'El cierre debe ser después de la apertura.'],
    [[{ start: '08:03', end: '12:00' }], 'Las horas deben ir de 5 en 5 minutos.'],
    [[{ start: '', end: '12:00' }], 'Completa las horas.'],
  ])('rechaza %j', (slots, message) => {
    expect(validateDay(slots)).toBe(message);
  });
});

describe('summarizeWeek', () => {
  const week = (days: string[], start: string, end: string): TimeRange[] =>
    days.map((d) => ({ dayOfWeek: d as TimeRange['dayOfWeek'], start, end }));

  it('une los días seguidos con el mismo horario', () => {
    const ranges = [
      ...week(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'], '08:00', '12:00'),
      ...week(['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'], '14:00', '18:00'),
      ...week(['Saturday'], '09:00', '13:00'),
    ];
    expect(summarizeWeek(ranges)).toBe('Lun–Vie 8:00–12:00, 14:00–18:00 · Sáb 9:00–13:00');
  });

  it('no une días con horarios distintos ni salta días libres', () => {
    const ranges = [...week(['Monday', 'Wednesday'], '08:00', '12:00')];
    expect(summarizeWeek(ranges)).toBe('Lun 8:00–12:00 · Mié 8:00–12:00');
  });

  it('queda vacío sin horario', () => {
    expect(summarizeWeek([])).toBe('');
  });
});

describe('bloqueos', () => {
  it('días completos van de las 00:00 del primero a las 00:00 del día siguiente al último', () => {
    expect(buildTimeOffRange(true, '2099-12-31', '', '2099-12-31', '')).toEqual({
      startsAt: '2099-12-31T00:00',
      endsAt: '2100-01-01T00:00',
    });
  });

  it('con horas usa las fechas y horas tal cual', () => {
    expect(buildTimeOffRange(false, '2099-03-02', '08:00', '2099-03-02', '12:30')).toEqual({
      startsAt: '2099-03-02T08:00',
      endsAt: '2099-03-02T12:30',
    });
  });

  it('describe un día completo, varios días y un rato', () => {
    expect(describeRange('2099-12-25T00:00:00', '2099-12-26T00:00:00')).not.toContain('–');
    expect(describeRange('2099-03-02T00:00:00', '2099-03-07T00:00:00')).toContain('–');
    expect(describeRange('2099-03-02T08:00:00', '2099-03-02T12:30:00')).toContain('8:00–12:30');
  });

  it('toLocalIso no cambia la hora por la zona del navegador', () => {
    expect(toLocalIso(new Date(2099, 0, 5, 8, 30))).toBe('2099-01-05T08:30');
  });
});
