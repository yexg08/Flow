import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroMoon, heroSun } from '@ng-icons/heroicons/outline';
import { ThemeService } from '../core/theme/theme.service';

@Component({
  selector: 'app-theme-toggle',
  imports: [NgIcon],
  providers: [provideIcons({ heroMoon, heroSun })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="btn-ghost px-2.5"
      (click)="theme.toggle()"
      [attr.aria-label]="theme.isDark() ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'"
      [attr.title]="theme.isDark() ? 'Modo claro' : 'Modo oscuro'"
    >
      <ng-icon [name]="theme.isDark() ? 'heroSun' : 'heroMoon'" size="20" />
    </button>
  `,
})
export class ThemeToggle {
  protected readonly theme = inject(ThemeService);
}
