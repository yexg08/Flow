import { Dialog } from '@angular/cdk/dialog';
import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroArrowPath, heroClock, heroKey, heroPencilSquare, heroPlus, heroTrash, heroUsers, heroXMark } from '@ng-icons/heroicons/outline';
import { filter, switchMap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { API_BASE } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { ConfirmService, DIALOG_DEFAULTS } from '../../../shared/confirm';
import { formatDate, readableTextOn } from '../../../shared/format';
import { GrantAccessDialog, StaffAccount, TemporaryPassword, TemporaryPasswordDialog } from './access-dialogs';
import { summarizeWeek } from '../../../shared/schedule';
import { Service, StaffMember } from '../business.models';
import { StaffDialog, StaffDialogData } from './staff-dialog';

@Component({
  selector: 'app-team-page',
  imports: [RouterLink, NgIcon],
  providers: [provideIcons({ heroArrowPath, heroClock, heroKey, heroPencilSquare, heroPlus, heroTrash, heroUsers, heroXMark })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './team-page.html',
})
export class TeamPage {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly confirm = inject(ConfirmService);
  private readonly toasts = inject(ToastService);

  protected readonly staff = httpResource<StaffMember[]>(() => `${API_BASE}/staff`, { defaultValue: [] });
  /** Solo el dueño ve y gestiona las cuentas del equipo. */
  private readonly accounts = httpResource<StaffAccount[]>(() => (this.auth.isOwner() ? `${API_BASE}/staff/accounts` : undefined), {
    defaultValue: [],
  });
  protected readonly accountOf = computed(() => new Map(this.accounts.value().map((a) => [a.staffMemberId, a])));
  protected readonly date = formatDate;

  protected grantAccess(person: StaffMember): void {
    this.dialog
      .open<TemporaryPassword>(GrantAccessDialog, { data: { staffId: person.id, name: person.name }, ...DIALOG_DEFAULTS })
      .closed.pipe(filter((result): result is TemporaryPassword => !!result))
      .subscribe((result) => {
        this.accounts.reload();
        this.showPassword(person, result);
      });
  }

  protected resetPassword(person: StaffMember): void {
    this.confirm
      .ask({
        title: `¿Nueva contraseña temporal para ${person.name}?`,
        message: 'La contraseña actual deja de servir y se cierran sus sesiones abiertas.',
        confirmText: 'Generar',
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.http.post<TemporaryPassword>(`${API_BASE}/staff/${person.id}/account/reset-password`, null)),
      )
      .subscribe((result) => {
        this.accounts.reload();
        this.showPassword(person, result);
      });
  }

  protected revokeAccess(person: StaffMember): void {
    this.confirm
      .ask({
        title: `¿Quitarle el acceso al panel a ${person.name}?`,
        message: 'Su cuenta se borra y sus sesiones se cierran de inmediato. Sigue en el equipo y en la agenda.',
        confirmText: 'Quitar acceso',
        danger: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.http.delete<void>(`${API_BASE}/staff/${person.id}/account`)),
      )
      .subscribe(() => {
        this.toasts.success(`${person.name} ya no tiene acceso al panel.`);
        this.accounts.reload();
      });
  }

  private showPassword(person: StaffMember, result: TemporaryPassword): void {
    this.dialog.open(TemporaryPasswordDialog, {
      data: { name: person.name, business: this.auth.tenant()?.name ?? 'tu negocio', result },
      ...DIALOG_DEFAULTS,
      disableClose: true,
    });
  }
  private readonly services = httpResource<Service[]>(() => `${API_BASE}/services`, { defaultValue: [] });

  /** Nombre de cada servicio por id, para mostrar los de cada persona. */
  private readonly serviceNames = computed(() => new Map(this.services.value().map((s) => [s.id, s.name])));

  protected readonly summarize = summarizeWeek;
  protected readonly textOn = readableTextOn;

  protected servicesOf(person: StaffMember): string[] {
    const names = this.serviceNames();
    return person.serviceIds.map((id) => names.get(id)).filter((n): n is string => !!n);
  }

  protected open(person: StaffMember | null = null): void {
    const data: StaffDialogData = { staff: person, services: this.services.value() };
    this.dialog
      .open<StaffMember>(StaffDialog, { data, ...DIALOG_DEFAULTS })
      .closed.pipe(filter((saved): saved is StaffMember => !!saved))
      .subscribe((saved) => {
        this.toasts.success(person ? `${saved.name} actualizado.` : `${saved.name} agregado al equipo.`);
        this.staff.reload();
      });
  }

  protected remove(person: StaffMember): void {
    this.confirm
      .ask({
        title: `¿Eliminar a ${person.name}?`,
        message:
          'Se borran también su horario, sus bloqueos y su acceso al panel. Si solo dejó de atender por un tiempo, mejor edítalo y desactívalo.',
        confirmText: 'Eliminar',
        danger: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.http.delete<void>(`${API_BASE}/staff/${person.id}`)),
      )
      .subscribe(() => {
        this.toasts.success(`${person.name} eliminado del equipo.`);
        this.staff.reload();
      });
  }
}
