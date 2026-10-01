import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroCalendarDays, heroChatBubbleLeftRight, heroClock } from '@ng-icons/heroicons/outline';
import { API_BASE } from '../../core/http/api';
import { formatDuration, formatPrice, readableTextOn } from '../../shared/format';
import { Logo } from '../../shared/logo';
import { ThemeToggle } from '../../shared/theme-toggle';
import { NotFound } from '../not-found';

interface PublicService {
  id: string;
  name: string;
  description: string | null;
  durationMinutes: number;
  price: number;
}

interface PublicBusinessPage {
  name: string;
  slug: string;
  accentColor: string;
  currency: string;
  whatsApp: string | null;
  services: PublicService[];
}

/** Página pública de un negocio: flow.app/n/{slug}. Sin sesión. */
@Component({
  selector: 'app-public-business',
  imports: [RouterLink, NgIcon, Logo, ThemeToggle, NotFound],
  providers: [provideIcons({ heroCalendarDays, heroChatBubbleLeftRight, heroClock })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './public-business.html',
})
export class PublicBusiness {
  /** Viene de la ruta n/:slug (withComponentInputBinding). */
  readonly slug = input.required<string>();

  protected readonly page = httpResource<PublicBusinessPage>(() => `${API_BASE}/public/businesses/${encodeURIComponent(this.slug())}`);

  protected readonly accent = computed(() => (this.page.hasValue() ? this.page.value().accentColor : '#a855f7'));
  protected readonly accentText = computed(() => readableTextOn(this.accent()));
  protected readonly whatsAppUrl = computed(() => {
    if (!this.page.hasValue() || !this.page.value().whatsApp) return null;
    const text = encodeURIComponent(`Hola, vi tu página en Flow y quiero información.`);
    return `https://wa.me/${this.page.value().whatsApp}?text=${text}`;
  });
  protected readonly initial = computed(() => (this.page.hasValue() ? this.page.value().name.charAt(0).toUpperCase() : ''));

  protected readonly formatDuration = formatDuration;
  protected readonly formatPrice = formatPrice;

  constructor() {
    const title = inject(Title);
    effect(() => {
      if (this.page.hasValue()) title.setTitle(`${this.page.value().name} · Reserva en línea`);
    });
  }
}
