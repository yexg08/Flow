import { formatTime, parseLocal } from './schedule';

export interface AppointmentMessageData {
  customerName: string;
  businessName: string;
  serviceName: string;
  staffName: string;
  /** Hora local del negocio: "2026-10-05T09:00:00". */
  startsAt: string;
  /** Enlace privado de la cita. */
  link: string;
}

const describeWhen = (startsAt: string): string => {
  const day = new Intl.DateTimeFormat('es-CO', { weekday: 'long', day: 'numeric', month: 'long' }).format(parseLocal(startsAt));
  return `el ${day} a las ${formatTime(startsAt.slice(11, 16))}`;
};

const firstName = (name: string): string => name.trim().split(/\s+/)[0] ?? name;

export function confirmationMessage(d: AppointmentMessageData): string {
  return (
    `Hola ${firstName(d.customerName)}, tu cita en ${d.businessName} quedó agendada: ${d.serviceName} ${describeWhen(d.startsAt)} con ${d.staffName}.\n\n` +
    `Para ver, reprogramar o cancelar tu cita: ${d.link}`
  );
}

export function reminderMessage(d: AppointmentMessageData): string {
  return (
    `Hola ${firstName(d.customerName)}, te recordamos tu cita en ${d.businessName}: ${d.serviceName} ${describeWhen(d.startsAt)} con ${d.staffName}.\n\n` +
    `Si necesitas cambiarla o cancelarla: ${d.link}`
  );
}

/** Enlace wa.me con el mensaje ya escrito. El número debe venir con indicativo (ver whatsAppNumber). */
export function whatsAppLink(number: string, message: string): string {
  return `https://wa.me/${number}?text=${encodeURIComponent(message)}`;
}
