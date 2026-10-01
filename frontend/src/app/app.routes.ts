import { Routes } from '@angular/router';
import { guestGuard, ownerGuard, superAdminGuard, tenantGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', title: 'Flow · Agenda en línea para tu negocio', loadComponent: () => import('./features/landing/landing').then((m) => m.Landing) },
  { path: 'login', title: 'Iniciar sesión · Flow', canActivate: [guestGuard], loadComponent: () => import('./features/auth/login').then((m) => m.Login) },
  { path: 'registro', title: 'Crea tu negocio · Flow', canActivate: [guestGuard], loadComponent: () => import('./features/auth/register').then((m) => m.Register) },
  {
    path: 'app',
    canActivate: [tenantGuard],
    loadComponent: () => import('./features/app/shell').then((m) => m.Shell),
    children: [
      { path: '', title: 'Inicio · Flow', loadComponent: () => import('./features/app/home').then((m) => m.Home) },
      { path: 'servicios', title: 'Servicios · Flow', loadComponent: () => import('./features/app/services/services-page').then((m) => m.ServicesPage) },
      { path: 'ajustes', title: 'Ajustes · Flow', canActivate: [ownerGuard], loadComponent: () => import('./features/app/settings/settings').then((m) => m.Settings) },
    ],
  },
  { path: 'admin', title: 'Plataforma · Flow', canActivate: [superAdminGuard], loadComponent: () => import('./features/admin/admin').then((m) => m.Admin) },
  { path: 'n/:slug', loadComponent: () => import('./features/public/public-business').then((m) => m.PublicBusiness) },
  { path: '**', title: 'Página no encontrada · Flow', loadComponent: () => import('./features/not-found').then((m) => m.NotFound) },
];
