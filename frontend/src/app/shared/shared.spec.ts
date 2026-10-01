import { formatDuration, readableTextOn } from './format';
import { hasValidSlugFormat, slugFromName } from './slug';

describe('slugFromName (mismas reglas que Slugs.cs en el backend)', () => {
  it.each([
    ['Peluquería Blue', 'peluqueria-blue'],
    ['  Café & Té  ', 'cafe-te'],
    ['Dra. María Núñez', 'dra-maria-nunez'],
    ['Taller 24/7', 'taller-24-7'],
  ])('%s → %s', (name, expected) => {
    expect(slugFromName(name)).toBe(expected);
  });

  it('corta en 40 caracteres sin dejar un guion al final', () => {
    const slug = slugFromName('Centro de estética y bienestar integral de la avenida Santander');
    expect(slug.length).toBeLessThanOrEqual(40);
    expect(slug.endsWith('-')).toBe(false);
  });
});

describe('hasValidSlugFormat', () => {
  it.each([
    ['peluqueria-blue', true],
    ['abc', true],
    ['ab', false],
    ['con--doble', false],
    ['-inicio', false],
    ['Mayus', false],
    ['con espacio', false],
  ])('%s → %s', (slug, expected) => {
    expect(hasValidSlugFormat(slug)).toBe(expected);
  });
});

describe('readableTextOn', () => {
  it('usa texto blanco sobre colores oscuros', () => {
    expect(readableTextOn('#1f2937')).toBe('#ffffff');
    expect(readableTextOn('#7c3aed')).toBe('#ffffff');
  });

  it('usa texto oscuro sobre colores claros', () => {
    expect(readableTextOn('#f59e0b')).toBe('#111111');
    expect(readableTextOn('#22c55e')).toBe('#111111');
  });
});

describe('formatDuration', () => {
  it.each([
    [30, '30 min'],
    [60, '1 h'],
    [90, '1 h 30 min'],
  ])('%i → %s', (minutes, expected) => {
    expect(formatDuration(minutes)).toBe(expected);
  });
});
