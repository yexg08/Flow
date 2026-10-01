import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { heroCheckCircle, heroExclamationCircle, heroEye, heroEyeSlash } from '@ng-icons/heroicons/outline';
import { catchError, debounceTime, distinctUntilChanged, map, of, startWith, switchMap } from 'rxjs';
import { SlugAvailability } from '../../core/auth/auth.models';
import { AuthService } from '../../core/auth/auth.service';
import { getErrorMessage, getFieldErrors } from '../../core/http/api';
import { hasValidSlugFormat, slugFromName } from '../../shared/slug';
import { AuthLayout } from './auth-layout';

type SlugState = { status: 'idle' } | { status: 'checking' } | { status: 'done'; result: SlugAvailability };

const PASSWORD_RULE = /^(?=.*[A-Za-zÀ-ÿ])(?=.*\d).{10,}$/;

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink, NgIcon, AuthLayout],
  providers: [provideIcons({ heroCheckCircle, heroExclamationCircle, heroEye, heroEyeSlash })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly form = inject(NonNullableFormBuilder).group({
    businessName: ['', [Validators.required, Validators.maxLength(80)]],
    slug: ['', [Validators.required]],
    fullName: ['', [Validators.required, Validators.maxLength(80)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]],
  });

  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string>>({});
  protected readonly showPassword = signal(false);
  protected readonly host = location.host;

  /** Mientras el usuario no edite el enlace a mano, se arma solo a partir del nombre del negocio. */
  private slugEditedByHand = false;

  protected readonly slugValue = toSignal(this.form.controls.slug.valueChanges, { initialValue: '' });

  /** Disponibilidad del enlace, consultada a la API con una pausa para no preguntar en cada tecla. */
  protected readonly slugState = toSignal(
    this.form.controls.slug.valueChanges.pipe(
      debounceTime(350),
      distinctUntilChanged(),
      switchMap((slug) => {
        if (!hasValidSlugFormat(slug)) return of<SlugState>({ status: 'idle' });
        return this.auth.checkSlug(slug).pipe(
          map((result): SlugState => ({ status: 'done', result })),
          startWith<SlugState>({ status: 'checking' }),
          catchError(() => of<SlugState>({ status: 'idle' })),
        );
      }),
    ),
    { initialValue: { status: 'idle' } as SlugState },
  );

  constructor() {
    const destroyRef = inject(DestroyRef);
    this.form.controls.businessName.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe((name) => {
      if (!this.slugEditedByHand) this.form.controls.slug.setValue(slugFromName(name));
    });
    // Un error del servidor deja de aplicar en cuanto el usuario cambia algo.
    this.form.valueChanges.pipe(takeUntilDestroyed(destroyRef)).subscribe(() => {
      if (Object.keys(this.serverErrors()).length > 0) this.serverErrors.set({});
    });
  }

  protected onSlugInput(): void {
    this.slugEditedByHand = true;
    const control = this.form.controls.slug;
    const cleaned = control.value.toLowerCase().replace(/\s+/g, '-');
    if (cleaned !== control.value) control.setValue(cleaned);
  }

  protected slugFormatError(): boolean {
    const value = this.slugValue();
    return value.length > 0 && !hasValidSlugFormat(value);
  }

  protected invalid(control: 'businessName' | 'slug' | 'fullName' | 'email' | 'password'): boolean {
    const c = this.form.controls[control];
    return (c.invalid && c.touched) || !!this.serverErrors()[control];
  }

  protected submit(): void {
    const state = this.slugState();
    const slugTaken = state.status === 'done' && !state.result.available;
    if (this.form.invalid || slugTaken || this.slugFormatError()) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.serverErrors.set({});
    this.auth.register(this.form.getRawValue()).subscribe({
      next: () => void this.router.navigateByUrl('/app'),
      error: (e: unknown) => {
        const fields = getFieldErrors(e);
        this.serverErrors.set(fields);
        if (Object.keys(fields).length === 0) this.error.set(getErrorMessage(e));
        this.loading.set(false);
      },
    });
  }
}
