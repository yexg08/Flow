using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>Un negocio registrado en Flow. Sus datos quedan aislados de los demás negocios.</summary>
public class Tenant : Entity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Identificador en la URL pública: flow.app/n/{slug}. Único, en minúsculas.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Zona horaria IANA en la que trabaja el negocio.</summary>
    public string TimeZone { get; set; } = "America/Bogota";

    public string Currency { get; set; } = "COP";

    /// <summary>Número de WhatsApp con indicativo, solo dígitos (p. ej. 573001234567).</summary>
    public string? WhatsApp { get; set; }

    /// <summary>Color de acento de la página pública, en hex (#rrggbb).</summary>
    public string AccentColor { get; set; } = "#a855f7";

    /// <summary>
    /// Un negocio suspendido (por el superadmin) no puede iniciar sesión, sus sesiones abiertas se cortan de
    /// inmediato y su página pública deja de existir. No se borra nada: se puede reactivar.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
