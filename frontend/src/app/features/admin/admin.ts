import { HttpClient, HttpParams, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import {
  heroArrowRightOnRectangle,
  heroArrowTopRightOnSquare,
  heroBuildingStorefront,
  heroMagnifyingGlass,
  heroSparkles,
  heroUsers,
  heroCheckBadge,
} from '@ng-icons/heroicons/outline';
import { debounceTime, filter, map, switchMap } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { API_BASE } from '../../core/http/api';
import { ToastService } from '../../core/notifications/toast.service';
import { ConfirmService } from '../../shared/confirm';
import { formatDate } from '../../shared/format';
import { Logo } from '../../shared/logo';
import { ThemeToggle } from '../../shared/theme-toggle';

interface AdminTenant {
  id: string;
  name: string;
  slug: string;
  ownerName: string;
  ownerEmail: string;
  createdAt: string;
  isActive: boolean;
  serviceCount: number;
  lastLoginAt: string | null;
}

interface AdminStats {
  businesses: number;
  activeBusinesses: number;
  newLast30Days: number;
  users: number;
}

/** Panel de la plataforma (solo superadmin): todos los negocios, sus dueños y la opción de suspenderlos. */
@Component({
  selector: 'app-admin',
  imports: [ReactiveFormsModule, RouterLink, NgIcon, Logo, ThemeToggle],
  providers: [
    provideIcons({
      heroArrowRightOnRectangle,
      heroArrowTopRightOnSquare,
      heroBuildingStorefront,
      heroMagnifyingGlass,
      heroSparkles,
      heroUsers,
      heroCheckBadge,
    }),
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin.html',
})
export class Admin {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly confirm = inject(ConfirmService);
  private readonly toasts = inject(ToastService);

  protected readonly search = new FormControl('', { nonNullable: true });
  private readonly term = toSignal(this.search.valueChanges.pipe(debounceTime(300), map((v) => v.trim())), { initialValue: '' });

  protected readonly stats = httpResource<AdminStats>(() => `${API_BASE}/admin/stats`);
  protected readonly tenants = httpResource<AdminTenant[]>(
    () => ({ url: `${API_BASE}/admin/tenants`, params: new HttpParams().set('search', this.term()) }),
    { defaultValue: [] },
  );

  protected readonly tiles = computed(() => {
    const s = this.stats.hasValue() ? this.stats.value() : null;
    return [
      { label: 'Negocios', value: s?.businesses, icon: 'heroBuildingStorefront' },
      { label: 'Activos', value: s?.activeBusinesses, icon: 'heroCheckBadge' },
      { label: 'Nuevos (30 días)', value: s?.newLast30Days, icon: 'heroSparkles' },
      { label: 'Cuentas de negocios', value: s?.users, icon: 'heroUsers' },
    ];
  });

  protected readonly busyId = signal<string | null>(null);
  protected readonly formatDate = formatDate;

  protected toggle(tenant: AdminTenant): void {
    const suspending = tenant.isActive;
    this.confirm
      .ask(
        suspending
          ? {
              title: `¿Suspender "${tenant.name}"?`,
              message: 'Sus sesiones se cerrarán de inmediato, no podrán entrar y su página pública dejará de verse. No se borra nada.',
              confirmText: 'Suspender',
              danger: true,
            }
          : {
              title: `¿Reactivar "${tenant.name}"?`,
              message: 'Podrán volver a entrar y su página pública se verá de nuevo.',
              confirmText: 'Reactivar',
            },
      )
      .pipe(
        filter(Boolean),
        switchMap(() => {
          this.busyId.set(tenant.id);
          return this.http.patch<void>(`${API_BASE}/admin/tenants/${tenant.id}/status`, { isActive: !suspending });
        }),
      )
      .subscribe({
        next: () => {
          this.toasts.success(suspending ? `"${tenant.name}" suspendido.` : `"${tenant.name}" reactivado.`);
          this.busyId.set(null);
          this.tenants.reload();
          this.stats.reload();
        },
        error: () => this.busyId.set(null),
      });
  }
}
