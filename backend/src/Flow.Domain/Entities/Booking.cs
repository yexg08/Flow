using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>Cliente final de un negocio. Sin cuenta: se identifica por su celular dentro de cada negocio.</summary>
public class Customer : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Solo dígitos, con indicativo si lo escribió (p. ej. 573001234567).</summary>
    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Notas internas del negocio sobre el cliente (alergias, preferencias...). El cliente no las ve.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Última vez que el cliente autorizó en la página pública el uso de sus datos (Ley 1581). Null si solo lo ha
    /// registrado el negocio desde el panel: en ese caso la autorización la gestiona el negocio.
    /// </summary>
    public DateTime? PrivacyConsentAt { get; set; }
}

public enum AppointmentStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow
}

/// <summary>Por dónde entró la cita: la página pública o el panel del negocio.</summary>
public enum AppointmentSource
{
    Online,
    Panel
}

/// <summary>
/// Una cita. Dos citas activas (no canceladas) de la misma persona del equipo no se pueden cruzar: lo garantiza una
/// restricción de exclusión de PostgreSQL, así que ni dos reservas simultáneas pueden tomar el mismo horario.
/// </summary>
public class Appointment : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid StaffMemberId { get; set; }

    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

    public AppointmentSource Source { get; set; } = AppointmentSource.Online;

    /// <summary>Precio del servicio al momento de reservar (el servicio puede cambiar de precio después).</summary>
    public decimal Price { get; set; }

    public string? CustomerNote { get; set; }

    /// <summary>
    /// SHA-256 del token del enlace con el que el cliente ve, cancela o reprograma su cita. El token se deriva del id
    /// con una clave secreta del servidor (HMAC), así el negocio puede volver a enviarlo; en la base solo queda el hash,
    /// y sin la clave no se puede armar el enlace.
    /// </summary>
    public string ManageTokenHash { get; set; } = string.Empty;

    public DateTime? CancelledAt { get; set; }
}
