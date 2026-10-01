import { whatsAppNumber } from '../../shared/format';
import { nextDays } from './booking-widgets';

describe('nextDays', () => {
  it('arranca con hoy y mañana y sigue con días consecutivos, cruzando de mes', () => {
    const days = nextDays(5, new Date(2099, 0, 30)); // 30 de enero
    expect(days.map((d) => d.date)).toEqual(['2099-01-30', '2099-01-31', '2099-02-01', '2099-02-02', '2099-02-03']);
    expect(days[0].top).toBe('Hoy');
    expect(days[1].top).toBe('Mañana');
    expect(days[2].day).toBe(1);
  });
});

describe('whatsAppNumber', () => {
  it('completa con 57 un celular colombiano escrito sin indicativo', () => {
    expect(whatsAppNumber('300 123 4567')).toBe('573001234567');
  });

  it('deja igual un número que ya trae indicativo u otro formato', () => {
    expect(whatsAppNumber('+57 300 123 4567')).toBe('573001234567');
    expect(whatsAppNumber('6068851183')).toBe('6068851183');
  });
});
