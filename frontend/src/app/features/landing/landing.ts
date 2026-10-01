import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  heroArrowRight,
  heroCalendarDays,
  heroCheck,
  heroLink,
  heroLockClosed,
  heroShieldCheck,
  heroSparkles,
} from '@ng-icons/heroicons/outline';
import { AuthService } from '../../core/auth/auth.service';
import { Logo } from '../../shared/logo';
import { ThemeToggle } from '../../shared/theme-toggle';

@Component({
  selector: 'app-landing',
  imports: [RouterLink, NgIcon, Logo, ThemeToggle],
  providers: [provideIcons({ heroArrowRight, heroCalendarDays, heroCheck, heroLink, heroLockClosed, heroShieldCheck, heroSparkles })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './landing.html',
})
export class Landing {
  protected readonly auth = inject(AuthService);

  protected readonly features = [
    {
      icon: 'heroLink',
      title: 'Tu propia página de reservas',
      text: 'Cada negocio tiene su enlace, como flow.app/n/tu-negocio. Ponlo en Instagram, en tu WhatsApp o en un QR.',
    },
    {
      icon: 'heroCalendarDays',
      title: 'Una agenda sin cruces',
      text: 'Flow calcula los horarios libres de cada persona de tu equipo y no deja que dos citas caigan en la misma hora.',
    },
    {
      icon: 'heroLockClosed',
      title: 'Tus datos son solo tuyos',
      text: 'Cada negocio ve únicamente su información. Tus clientes y tu agenda nunca se mezclan con los de otro negocio.',
    },
  ];

  protected readonly steps = [
    { title: 'Crea tu negocio', text: 'Registro gratis en un minuto, con tu nombre y tu enlace.' },
    { title: 'Agrega tus servicios', text: 'Nombre, duración y precio de lo que ofreces.' },
    { title: 'Comparte tu enlace', text: 'Tus clientes reservan solos, sin llamarte ni escribirte.' },
  ];

  /** Solo ilustra cómo se ve una página de reservas: no son datos reales. */
  protected readonly previewServices = [
    { name: 'Corte clásico', meta: '30 min · $25.000' },
    { name: 'Corte + barba', meta: '45 min · $35.000' },
    { name: 'Afeitado con toalla caliente', meta: '30 min · $22.000' },
  ];

  protected readonly previewSlots = ['9:00', '9:30', '10:30', '11:00', '2:00', '3:30'];
}
