export interface Service {
  id: string;
  name: string;
  description: string | null;
  durationMinutes: number;
  price: number;
  isActive: boolean;
}

export type ServiceUpsert = Omit<Service, 'id'>;

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
