/** Precio en la moneda del negocio, sin decimales para monedas como el peso colombiano. */
export function formatPrice(value: number, currency = 'COP'): string {
  return new Intl.NumberFormat('es-CO', {
    style: 'currency',
    currency,
    maximumFractionDigits: currency === 'COP' ? 0 : 2,
  }).format(value);
}

/** 30 → "30 min", 90 → "1 h 30 min". */
export function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return `${rest} min`;
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}

/** Fecha corta en español de Colombia: "1 oct 2026". */
export function formatDate(iso: string | null): string {
  if (!iso) return '—';
  return new Intl.DateTimeFormat('es-CO', { day: 'numeric', month: 'short', year: 'numeric' }).format(new Date(iso));
}

/**
 * Color de texto legible (blanco o casi negro) sobre un color de fondo que elige el negocio:
 * se queda con el que dé más contraste según WCAG.
 */
export function readableTextOn(hex: string): '#ffffff' | '#111111' {
  const [r, g, b] = [1, 3, 5]
    .map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((v) => (v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  const luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
  const withWhite = 1.05 / (luminance + 0.05);
  const withBlack = (luminance + 0.05) / (0.0056 + 0.05);
  return withWhite >= withBlack ? '#ffffff' : '#111111';
}
