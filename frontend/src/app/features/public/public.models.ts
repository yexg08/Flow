export interface PublicService {
  id: string;
  name: string;
  description: string | null;
  durationMinutes: number;
  price: number;
}

export interface PublicStaff {
  id: string;
  name: string;
  color: string;
  serviceIds: string[];
}

export interface PublicBusinessPage {
  name: string;
  slug: string;
  accentColor: string;
  currency: string;
  whatsApp: string | null;
  services: PublicService[];
  staff: PublicStaff[];
}

export interface PublicSlot {
  /** "HH:mm", hora local del negocio. */
  time: string;
  staffIds: string[];
}

export interface PublicAvailability {
  date: string;
  slots: PublicSlot[];
}

export type AppointmentStatus = 'Confirmed' | 'Cancelled' | 'Completed' | 'NoShow';

/** Fechas en hora local del negocio, sin zona: "2026-10-05T09:00:00". */
export interface PublicAppointment {
  businessName: string;
  businessSlug: string;
  accentColor: string;
  businessWhatsApp: string | null;
  serviceId: string;
  serviceName: string;
  durationMinutes: number;
  price: number;
  currency: string;
  staffMemberId: string;
  staffName: string;
  staffColor: string;
  startsAt: string;
  endsAt: string;
  status: AppointmentStatus;
  canChange: boolean;
  customerName: string;
}

export interface BookingResult {
  manageToken: string;
  appointment: PublicAppointment;
}
