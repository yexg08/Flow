using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>Una persona del negocio que atiende citas (el barbero, la odontóloga...). No es una cuenta de usuario.</summary>
public class StaffMember : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Color con el que se distingue en la agenda, en hex (#rrggbb).</summary>
    public string Color { get; set; } = "#a855f7";

    /// <summary>Inactivo = no recibe reservas nuevas (vacaciones largas, ya no trabaja ahí...), pero se conserva.</summary>
    public bool IsActive { get; set; } = true;

    public List<StaffMemberService> Services { get; set; } = [];
    public List<WorkingHours> WorkingHours { get; set; } = [];
}

/// <summary>Qué servicios hace cada persona del equipo.</summary>
public class StaffMemberService : ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid StaffMemberId { get; set; }
    public Guid ServiceId { get; set; }
}

/// <summary>
/// Un tramo del horario semanal de una persona: p. ej. lunes de 08:00 a 12:00. Un mismo día puede tener varios
/// tramos (mañana y tarde). Las horas son locales del negocio (su zona horaria).
/// </summary>
public class WorkingHours : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TenantId { get; set; }
    public Guid StaffMemberId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; }
}

/// <summary>
/// Un bloqueo de tiempo en que no se puede reservar: vacaciones, una cita médica, un festivo. Sin persona = todo el
/// negocio está cerrado. Se guarda en UTC; la API recibe y devuelve la hora local del negocio.
/// </summary>
public class TimeOff : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid? StaffMemberId { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string? Reason { get; set; }
}
