import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree, provideRouter } from '@angular/router';
import { guestGuard, ownerGuard, passwordChangeGuard, superAdminGuard, tenantGuard } from './auth.guards';
import { AuthService } from './auth.service';
import { Role } from './auth.models';

/** AuthService falso con el rol que se quiera probar. */
function setup(role: Role | null, mustChangePassword = false) {
  const authenticated = role !== null;
  const fake = {
    isAuthenticated: signal(authenticated),
    isSuperAdmin: signal(role === 'SuperAdmin'),
    isOwner: signal(role === 'Owner'),
    isTenantMember: signal(role === 'Owner' || role === 'Staff'),
    mustChangePassword: signal(mustChangePassword),
    homeUrl: () =>
      !authenticated ? '/login' : mustChangePassword ? '/cambiar-contrasena' : role === 'SuperAdmin' ? '/admin' : '/app',
  };
  TestBed.configureTestingModule({ providers: [provideRouter([]), { provide: AuthService, useValue: fake }] });
}

function run(guard: typeof tenantGuard): boolean | string {
  const result = TestBed.runInInjectionContext(() =>
    guard({} as ActivatedRouteSnapshot, { url: '/destino' } as RouterStateSnapshot),
  );
  return result instanceof UrlTree ? TestBed.inject(Router).serializeUrl(result) : (result as boolean);
}

describe('guards', () => {
  it('el panel del negocio es para dueños y empleados', () => {
    setup('Owner');
    expect(run(tenantGuard)).toBe(true);
  });

  it('el superadmin no entra al panel de un negocio', () => {
    setup('SuperAdmin');
    expect(run(tenantGuard)).toBe('/admin');
  });

  it('sin sesión manda al login recordando a dónde iba', () => {
    setup(null);
    expect(run(tenantGuard)).toBe('/login?returnUrl=%2Fdestino');
  });

  it('un empleado no entra a los ajustes del negocio', () => {
    setup('Staff');
    expect(run(ownerGuard)).toBe('/app');
  });

  it('solo el superadmin entra a la plataforma', () => {
    setup('Owner');
    expect(run(superAdminGuard)).toBe('/app');
  });

  it('con sesión, login y registro mandan al panel', () => {
    setup('Owner');
    expect(run(guestGuard)).toBe('/app');
  });

  it('con contraseña temporal, el panel manda a cambiarla', () => {
    setup('Staff', true);
    expect(run(tenantGuard)).toBe('/cambiar-contrasena');
    expect(run(passwordChangeGuard)).toBe(true);
  });

  it('sin contraseña temporal, la pantalla de cambio no aplica', () => {
    setup('Staff');
    expect(run(passwordChangeGuard)).toBe('/app');
  });
});
