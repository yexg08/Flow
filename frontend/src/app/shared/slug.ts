/** Mismas reglas que el backend (Flow.Application/Common/Slugs.cs). La API valida igual: esto es para guiar al usuario. */
export const SLUG_MAX = 40;

const SLUG_FORMAT = /^(?=.{3,40}$)[a-z0-9]+(?:-[a-z0-9]+)*$/;

/** "Peluquería Blue" → "peluqueria-blue". */
export function slugFromName(name: string): string {
  const slug = name
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/-{2,}/g, '-')
    .replace(/^-|-$/g, '');
  return slug.length > SLUG_MAX ? slug.slice(0, SLUG_MAX).replace(/-+$/, '') : slug;
}

export function hasValidSlugFormat(slug: string): boolean {
  return SLUG_FORMAT.test(slug);
}

/** Enlace público del negocio en este mismo dominio. */
export function publicUrl(slug: string): string {
  return `${location.origin}/n/${slug}`;
}
