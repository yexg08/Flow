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

    /// <summary>Última vez que autorizó el uso de sus datos para gestionar sus citas (Ley 1581).</summary>
    public DateTime PrivacyConsentAt { get; set; }
}

public enum AppointmentStatus
{
    Confirmed,
    Cancelled,
    Completed,
    NoShow
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

    /// <summary>Precio del servicio al momento de reservar (el servicio puede cambiar de precio después).</summary>
    public decimal Price { get; set; }

    public string? CustomerNote { get; set; }

    /// <summary>
    /// SHA-256 del token del enlace que recibe el cliente para ver, cancelar o reprogramar su cita. El token en sí no
    /// se guarda: quien tenga acceso a la base no puede armar el enlace.
    /// </summary>
    public string ManageTokenHash { get; set; } = string.Empty;

    public DateTime? CancelledAt { get; set; }
}
