import { confirmationMessage, reminderMessage, whatsAppLink } from './messages';

const data = {
  customerName: 'Carlos Pérez',
  businessName: 'Barbería Norte',
  serviceName: 'Corte clásico',
  staffName: 'Ana',
  startsAt: '2099-03-02T09:30:00',
  link: 'https://flow.app/cita/abc',
};

describe('mensajes de WhatsApp', () => {
  it('la confirmación saluda por el primer nombre y trae fecha, hora, persona y enlace', () => {
    const text = confirmationMessage(data);
    expect(text.startsWith('Hola Carlos,')).toBe(true);
    expect(text).toContain('Corte clásico');
    expect(text).toContain('a las 9:30');
    expect(text).toContain('con Ana');
    expect(text).toContain('https://flow.app/cita/abc');
  });

  it('el recordatorio también trae el enlace para cambiarla', () => {
    expect(reminderMessage(data)).toContain('te recordamos tu cita en Barbería Norte');
    expect(reminderMessage(data)).toContain('https://flow.app/cita/abc');
  });

  it('el enlace de wa.me codifica el mensaje', () => {
    expect(whatsAppLink('573001234567', 'Hola & chao')).toBe('https://wa.me/573001234567?text=Hola%20%26%20chao');
  });
});
