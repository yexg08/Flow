import { ChangeDetectionStrategy, Component, input } from '@angular/core';

let nextId = 0;

/** Marca de Flow: una "F" inclinada con el degradado de la marca (inclinada = movimiento, "fluir"). */
@Component({
  selector: 'app-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'inline-flex items-center gap-2.5' },
  template: `
    <svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 32 32" aria-hidden="true" class="shrink-0">
      <defs>
        <linearGradient [attr.id]="gradientId" x1="6" y1="28" x2="27" y2="4" gradientUnits="userSpaceOnUse">
          <stop offset="0" stop-color="#7c3aed" />
          <stop offset="0.55" stop-color="#c084fc" />
          <stop offset="1" stop-color="#f472b6" />
        </linearGradient>
      </defs>
      <g [attr.fill]="'url(#' + gradientId + ')'" transform="translate(2.5 0) skewX(-10)">
        <rect x="9" y="6" width="5.5" height="20" rx="2.75" />
        <rect x="9" y="6" width="16" height="5.5" rx="2.75" />
        <rect x="9" y="14.2" width="11.5" height="5" rx="2.5" />
      </g>
    </svg>
    @if (withText()) {
      <span class="text-xl font-extrabold tracking-tight">Flow</span>
    }
  `,
})
export class Logo {
  readonly size = input(32);
  readonly withText = input(true);

  /** Id único por instancia: con varios logos en la página, un id repetido rompe el degradado. */
  protected readonly gradientId = `flow-logo-${++nextId}`;
}
