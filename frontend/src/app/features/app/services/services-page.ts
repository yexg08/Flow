import { Dialog } from '@angular/cdk/dialog';
import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroClock, heroEyeSlash, heroPencilSquare, heroPlus, heroSquares2x2, heroTrash } from '@ng-icons/heroicons/outline';
import { filter, switchMap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { API_BASE } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { ConfirmService, DIALOG_DEFAULTS } from '../../../shared/confirm';
import { formatDuration, formatPrice } from '../../../shared/format';
import { Service } from '../business.models';
import { ServiceDialog } from './service-dialog';

@Component({
  selector: 'app-services-page',
  imports: [NgIcon],
  providers: [provideIcons({ heroClock, heroEyeSlash, heroPencilSquare, heroPlus, heroSquares2x2, heroTrash })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './services-page.html',
})
export class ServicesPage {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly confirm = inject(ConfirmService);
  private readonly toasts = inject(ToastService);

  protected readonly services = httpResource<Service[]>(() => `${API_BASE}/services`, { defaultValue: [] });

  protected readonly formatPrice = formatPrice;
  protected readonly formatDuration = formatDuration;

  protected open(service: Service | null = null): void {
    this.dialog
      .open<Service>(ServiceDialog, { data: service, ...DIALOG_DEFAULTS })
      .closed.pipe(filter((saved): saved is Service => !!saved))
      .subscribe((saved) => {
        this.toasts.success(service ? `"${saved.name}" actualizado.` : `"${saved.name}" agregado.`);
        this.services.reload();
      });
  }

  protected remove(service: Service): void {
    this.confirm
      .ask({
        title: '¿Eliminar este servicio?',
        message: `"${service.name}" desaparecerá de tu página. Si solo quieres ocultarlo un tiempo, edítalo y desactívalo.`,
        confirmText: 'Eliminar',
        danger: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.http.delete<void>(`${API_BASE}/services/${service.id}`)),
      )
      .subscribe(() => {
        this.toasts.success(`"${service.name}" eliminado.`);
        this.services.reload();
      });
  }
}
