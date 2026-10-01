import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Toaster } from './core/notifications/toaster';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Toaster],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <router-outlet />
    <app-toaster />
  `,
})
export class App {}
