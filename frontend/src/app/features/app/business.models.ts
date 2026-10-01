import { TimeRange } from '../../shared/schedule';

export interface Service {
  id: string;
  name: string;
  description: string | null;
  durationMinutes: number;
  price: number;
  isActive: boolean;
}

export type ServiceUpsert = Omit<Service, 'id'>;

export interface StaffMember {
  id: string;
  name: string;
  color: string;
  isActive: boolean;
  serviceIds: string[];
  workingHours: TimeRange[];
}

export interface StaffUpsert {
  name: string;
  color: string;
  isActive: boolean;
  serviceIds: string[];
}

/** Las fechas son hora local del negocio, sin zona: "2026-10-05T08:00:00". */
export interface TimeOff {
  id: string;
  /** Null = todo el negocio. */
  staffMemberId: string | null;
  staffName: string | null;
  startsAt: string;
  endsAt: string;
  reason: string | null;
}

export interface TimeOffRequest {
  staffMemberId: string | null;
  startsAt: string;
  endsAt: string;
  reason: string | null;
}

export interface BusinessSettings {
  id: string;
  name: string;
  slug: string;
  timeZone: string;
  currency: string;
  whatsApp: string | null;
  accentColor: string;
}

export type UpdateBusiness = Pick<BusinessSettings, 'name' | 'slug' | 'timeZone' | 'whatsApp' | 'accentColor'>;
