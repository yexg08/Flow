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
      { path: 'agenda', title: 'Agenda · Flow', loadComponent: () => import('./features/app/agenda/agenda-page').then((m) => m.AgendaPage) },
      { path: 'clientes', title: 'Clientes · Flow', loadComponent: () => import('./features/app/customers/customers-page').then((m) => m.CustomersPage) },
      { path: 'clientes/:id', title: 'Cliente · Flow', loadComponent: () => import('./features/app/customers/customer-detail').then((m) => m.CustomerDetailPage) },
      { path: 'servicios', title: 'Servicios · Flow', loadComponent: () => import('./features/app/services/services-page').then((m) => m.ServicesPage) },
      { path: 'equipo', title: 'Equipo · Flow', loadComponent: () => import('./features/app/team/team-page').then((m) => m.TeamPage) },
      { path: 'equipo/:id/horario', title: 'Horario · Flow', loadComponent: () => import('./features/app/team/schedule-page').then((m) => m.SchedulePage) },
      { path: 'bloqueos', title: 'Bloqueos · Flow', loadComponent: () => import('./features/app/time-off/time-off-page').then((m) => m.TimeOffPage) },
      { path: 'ajustes', title: 'Ajustes · Flow', canActivate: [ownerGuard], loadComponent: () => import('./features/app/settings/settings').then((m) => m.Settings) },
    ],
  },
  { path: 'admin', title: 'Plataforma · Flow', canActivate: [superAdminGuard], loadComponent: () => import('./features/admin/admin').then((m) => m.Admin) },
  { path: 'n/:slug', loadComponent: () => import('./features/public/public-business').then((m) => m.PublicBusiness) },
  { path: 'n/:slug/reservar/:serviceId', loadComponent: () => import('./features/public/book-appointment').then((m) => m.BookAppointment) },
  { path: 'cita/:token', title: 'Tu cita · Flow', loadComponent: () => import('./features/public/manage-appointment').then((m) => m.ManageAppointment) },
  { path: '**', title: 'Página no encontrada · Flow', loadComponent: () => import('./features/not-found').then((m) => m.NotFound) },
];
