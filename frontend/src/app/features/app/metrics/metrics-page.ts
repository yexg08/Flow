import { HttpParams, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroArrowTrendingUp, heroCalendarDays, heroGlobeAlt, heroNoSymbol, heroUserPlus } from '@ng-icons/heroicons/outline';
import { API_BASE } from '../../../core/http/api';
import { formatPrice } from '../../../shared/format';
import { parseLocal } from '../../../shared/schedule';

interface DailyPoint {
  date: string;
  appointments: number;
  completed: number;
}

interface Ranking {
  name: string;
  color: string | null;
  appointments: number;
  revenue: number;
}

interface Metrics {
  from: string;
  to: string;
  currency: string;
  appointments: number;
  completed: number;
  noShows: number;
  cancelled: number;
  revenue: number;
  noShowRate: number;
  onlineShare: number;
  newCustomers: number;
  upcomingAppointments: number;
  upcomingRevenue: number;
  daily: DailyPoint[];
  topServices: Ranking[];
  byStaff: Ranking[];
}

const PERIODS = [
  { days: 7, label: '7 días' },
  { days: 30, label: '30 días' },
  { days: 90, label: '90 días' },
];

/** Escala "bonita" para el eje: el siguiente valor redondo (1, 2, 5, 10, 20, 50...) por encima del máximo. */
export function niceMax(value: number): number {
  if (value <= 4) return 4;
  const magnitude = 10 ** Math.floor(Math.log10(value));
  const step = [1, 2, 5, 10].find((s) => s * magnitude >= value) ?? 10;
  return step * magnitude;
}

@Component({
  selector: 'app-metrics-page',
  imports: [NgIcon],
  providers: [provideIcons({ heroArrowTrendingUp, heroCalendarDays, heroGlobeAlt, heroNoSymbol, heroUserPlus })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './metrics-page.html',
})
export class MetricsPage {
  protected readonly periods = PERIODS;
  protected readonly days = signal(30);
  protected readonly metrics = httpResource<Metrics>(() => ({
    url: `${API_BASE}/business/metrics`,
    params: new HttpParams().set('days', this.days()),
  }));
  protected readonly showTable = signal(false);

  protected readonly m = computed(() => (this.metrics.hasValue() ? this.metrics.value() : null));

  /** Eje Y de la gráfica diaria: máximo redondo y 4 líneas de guía. */
  protected readonly axis = computed(() => {
    const max = niceMax(Math.max(0, ...(this.m()?.daily.map((d) => d.appointments) ?? [0])));
    return { max, ticks: [1, 0.75, 0.5, 0.25].map((f) => Math.round(max * f)) };
  });

  /** Etiquetas del eje X: unas pocas, repartidas, para que no se encimen. */
  protected readonly labelEvery = computed(() => {
    const n = this.m()?.daily.length ?? 0;
    return n <= 10 ? 1 : n <= 31 ? 5 : 15;
  });

  protected readonly money = (value: number) => formatPrice(value, this.m()?.currency ?? 'COP');
  protected readonly percent = (value: number) => `${Math.round(value * 100)} %`;

  protected dayLabel(date: string, long = false): string {
    return new Intl.DateTimeFormat('es-CO', long ? { weekday: 'short', day: 'numeric', month: 'short' } : { day: 'numeric', month: 'short' })
      .format(parseLocal(`${date}T00:00`))
      .replace(/\./g, '');
  }

  protected barHeight(value: number): number {
    return (value / this.axis().max) * 100;
  }

  protected rankWidth(value: number, list: Ranking[]): number {
    const max = Math.max(1, ...list.map((r) => r.appointments));
    return (value / max) * 100;
  }
}
