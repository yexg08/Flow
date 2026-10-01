import { Dialog } from '@angular/cdk/dialog';
import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroClock, heroPencilSquare, heroPlus, heroTrash, heroUsers } from '@ng-icons/heroicons/outline';
import { filter, switchMap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { API_BASE } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { ConfirmService, DIALOG_DEFAULTS } from '../../../shared/confirm';
import { readableTextOn } from '../../../shared/format';
import { summarizeWeek } from '../../../shared/schedule';
import { Service, StaffMember } from '../business.models';
import { StaffDialog, StaffDialogData } from './staff-dialog';

@Component({
  selector: 'app-team-page',
  imports: [RouterLink, NgIcon],
  providers: [provideIcons({ heroClock, heroPencilSquare, heroPlus, heroTrash, heroUsers })],
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
        message: 'Se borran también su horario y sus bloqueos. Si solo dejó de atender por un tiempo, mejor edítalo y desactívalo.',
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
