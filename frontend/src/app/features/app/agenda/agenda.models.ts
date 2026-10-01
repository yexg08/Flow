import { TimeOff } from '../business.models';

export type AppointmentStatus = 'Confirmed' | 'Cancelled' | 'Completed' | 'NoShow';

/** Fechas en hora local del negocio, sin zona: "2026-10-05T09:00:00". */
export interface AgendaAppointment {
  id: string;
  startsAt: string;
  endsAt: string;
  status: AppointmentStatus;
  serviceId: string;
  serviceName: string;
  staffMemberId: string;
  staffName: string;
  staffColor: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  customerNote: string | null;
  price: number;
}

export interface Agenda {
  appointments: AgendaAppointment[];
  timeOff: TimeOff[];
}

export const STATUS_LABEL: Record<AppointmentStatus, string> = {
  Confirmed: 'Confirmada',
  Completed: 'Atendida',
  NoShow: 'No asistió',
  Cancelled: 'Cancelada',
};

/** Clases de la etiqueta de cada estado (colores de texto con contraste verificado en ambos temas). */
export const STATUS_BADGE: Record<AppointmentStatus, string> = {
  Confirmed: 'bg-violet-500/15 text-accent',
  Completed: 'bg-success/15 text-success',
  NoShow: 'bg-danger/15 text-danger',
  Cancelled: 'bg-surface-2 text-muted',
};
