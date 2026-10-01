import { Dialog } from '@angular/cdk/dialog';
import { HttpClient, httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroBuildingStorefront, heroNoSymbol, heroPlus, heroTrash } from '@ng-icons/heroicons/outline';
import { filter, switchMap } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { API_BASE } from '../../../core/http/api';
import { ToastService } from '../../../core/notifications/toast.service';
import { ConfirmService, DIALOG_DEFAULTS } from '../../../shared/confirm';
import { readableTextOn } from '../../../shared/format';
import { describeRange } from '../../../shared/schedule';
import { StaffMember, TimeOff } from '../business.models';
import { TimeOffDialog } from './time-off-dialog';

@Component({
  selector: 'app-time-off-page',
  imports: [NgIcon],
  providers: [provideIcons({ heroBuildingStorefront, heroNoSymbol, heroPlus, heroTrash })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto max-w-4xl">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 class="text-2xl font-extrabold tracking-tight sm:text-3xl">Bloqueos</h1>
          <p class="mt-1 text-muted">Vacaciones, festivos o ratos en que no se puede reservar.</p>
        </div>
        @if (auth.isOwner()) {
          <button type="button" class="btn-primary" (click)="open()"><ng-icon name="heroPlus" size="18" /> Nuevo bloqueo</button>
        }
      </div>

      @if (blocks.isLoading() && blocks.value().length === 0) {
        <div class="card mt-8 h-40 animate-pulse bg-surface-2" aria-label="Cargando bloqueos"></div>
      } @else if (blocks.error()) {
        <div class="card mt-8 p-8 text-center">
          <p class="font-semibold">No se pudieron cargar los bloqueos.</p>
          <button type="button" class="btn-secondary mt-4" (click)="blocks.reload()">Reintentar</button>
        </div>
      } @else if (blocks.value().length === 0) {
        <div class="card mt-8 flex flex-col items-center px-6 py-14 text-center">
          <div class="flex size-14 items-center justify-center rounded-2xl bg-violet-500/15 text-accent">
            <ng-icon name="heroNoSymbol" size="28" />
          </div>
          <h2 class="mt-5 text-lg font-bold">No hay bloqueos próximos</h2>
          <p class="mt-1 max-w-sm text-sm text-muted">Si alguien sale de vacaciones o el negocio cierra un festivo, agrégalo aquí para que nadie reserve.</p>
        </div>
      } @else {
        <ul class="mt-8 space-y-3">
          @for (block of blocks.value(); track block.id) {
            <li class="card flex items-center gap-4 p-4 sm:p-5">
              @if (block.staffMemberId) {
                <span
                  class="flex size-10 shrink-0 items-center justify-center rounded-full text-sm font-bold"
                  [style.background-color]="colorOf(block.staffMemberId)"
                  [style.color]="textOn(colorOf(block.staffMemberId))"
                  aria-hidden="true"
                >
                  {{ block.staffName?.charAt(0)?.toUpperCase() }}
                </span>
              } @else {
                <span class="flex size-10 shrink-0 items-center justify-center rounded-full bg-surface-2 text-muted" aria-hidden="true">
                  <ng-icon name="heroBuildingStorefront" size="20" />
                </span>
              }
              <div class="min-w-0 flex-1">
                <p class="font-semibold">{{ describe(block.startsAt, block.endsAt) }}</p>
                <p class="text-sm text-muted">
                  {{ block.staffName ?? 'Todo el negocio' }}@if (block.reason) { · {{ block.reason }} }
                </p>
              </div>
              @if (auth.isOwner()) {
                <button type="button" class="btn-ghost px-2.5 hover:text-danger" (click)="remove(block)" aria-label="Eliminar bloqueo">
                  <ng-icon name="heroTrash" size="18" />
                </button>
              }
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class TimeOffPage {
  protected readonly auth = inject(AuthService);
  private readonly http = inject(HttpClient);
  private readonly dialog = inject(Dialog);
  private readonly confirm = inject(ConfirmService);
  private readonly toasts = inject(ToastService);

  protected readonly blocks = httpResource<TimeOff[]>(() => `${API_BASE}/time-off`, { defaultValue: [] });
  private readonly staff = httpResource<StaffMember[]>(() => `${API_BASE}/staff`, { defaultValue: [] });
  private readonly colors = computed(() => new Map(this.staff.value().map((s) => [s.id, s.color])));

  protected readonly describe = describeRange;
  protected readonly textOn = readableTextOn;

  protected colorOf(staffId: string): string {
    return this.colors().get(staffId) ?? '#6e6884';
  }

  protected open(): void {
    this.dialog
      .open<TimeOff>(TimeOffDialog, { data: this.staff.value(), ...DIALOG_DEFAULTS })
      .closed.pipe(filter((saved): saved is TimeOff => !!saved))
      .subscribe(() => {
        this.toasts.success('Bloqueo guardado.');
        this.blocks.reload();
      });
  }

  protected remove(block: TimeOff): void {
    this.confirm
      .ask({
        title: '¿Eliminar este bloqueo?',
        message: `${block.staffName ?? 'Todo el negocio'} · ${describeRange(block.startsAt, block.endsAt)}. Ese tiempo volverá a estar disponible para reservar.`,
        confirmText: 'Eliminar',
        danger: true,
      })
      .pipe(
        filter(Boolean),
        switchMap(() => this.http.delete<void>(`${API_BASE}/time-off/${block.id}`)),
      )
      .subscribe(() => {
        this.toasts.success('Bloqueo eliminado.');
        this.blocks.reload();
      });
  }
}
