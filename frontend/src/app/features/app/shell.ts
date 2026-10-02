import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  heroArrowRightOnRectangle,
  heroArrowTopRightOnSquare,
  heroBars3,
  heroCalendarDays,
  heroChartBar,
  heroCog6Tooth,
  heroHome,
  heroNoSymbol,
  heroSquares2x2,
  heroUserGroup,
  heroUsers,
  heroXMark,
} from '@ng-icons/heroicons/outline';
import { filter } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Logo } from '../../shared/logo';
import { ThemeToggle } from '../../shared/theme-toggle';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  exact: boolean;
  ownerOnly: boolean;
}

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, NgIcon, Logo, ThemeToggle],
  providers: [
    provideIcons({
      heroArrowRightOnRectangle,
      heroArrowTopRightOnSquare,
      heroBars3,
      heroCalendarDays,
      heroChartBar,
      heroCog6Tooth,
      heroHome,
      heroNoSymbol,
      heroSquares2x2,
      heroUserGroup,
      heroUsers,
      heroXMark,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly menuOpen = signal(false);

  private readonly items: NavItem[] = [
    { path: '/app', label: 'Inicio', icon: 'heroHome', exact: true, ownerOnly: false },
    { path: '/app/agenda', label: 'Agenda', icon: 'heroCalendarDays', exact: false, ownerOnly: false },
    { path: '/app/clientes', label: 'Clientes', icon: 'heroUserGroup', exact: false, ownerOnly: false },
    { path: '/app/servicios', label: 'Servicios', icon: 'heroSquares2x2', exact: false, ownerOnly: false },
    { path: '/app/equipo', label: 'Equipo y horarios', icon: 'heroUsers', exact: false, ownerOnly: false },
    { path: '/app/bloqueos', label: 'Bloqueos', icon: 'heroNoSymbol', exact: false, ownerOnly: false },
    { path: '/app/metricas', label: 'Métricas', icon: 'heroChartBar', exact: false, ownerOnly: true },
    { path: '/app/ajustes', label: 'Ajustes del negocio', icon: 'heroCog6Tooth', exact: false, ownerOnly: true },
  ];

  protected readonly nav = computed(() => this.items.filter((item) => !item.ownerOnly || this.auth.isOwner()));
  protected readonly roleLabel = computed(() => (this.auth.isOwner() ? 'Dueño' : 'Equipo'));

  constructor() {
    // En el celular, el menú se cierra al navegar.
    inject(Router)
      .events.pipe(
        filter((e) => e instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe(() => this.menuOpen.set(false));
  }
}
