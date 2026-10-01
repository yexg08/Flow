import { DOCUMENT, Injectable, computed, effect, inject, signal } from '@angular/core';

export type Theme = 'light' | 'dark';

const STORAGE_KEY = 'flow-theme';

/**
 * Tema claro/oscuro. El script de index.html aplica el tema guardado (o el del sistema) antes de cargar Angular;
 * este servicio parte de ese estado y lo mantiene sincronizado con la clase .dark de <html>.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly root = inject(DOCUMENT).documentElement;

  readonly theme = signal<Theme>(this.root.classList.contains('dark') ? 'dark' : 'light');
  readonly isDark = computed(() => this.theme() === 'dark');

  constructor() {
    effect(() => this.root.classList.toggle('dark', this.isDark()));
  }

  toggle(): void {
    const next: Theme = this.isDark() ? 'light' : 'dark';
    this.theme.set(next);
    try {
      localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Almacenamiento bloqueado (modo privado): el tema funciona igual, solo no se recuerda.
    }
  }
}
