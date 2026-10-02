using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    // Todos los siguientes se filtran automáticamente al negocio de la sesión.
    DbSet<BookableService> Services { get; }
    DbSet<StaffMember> Staff { get; }
    DbSet<StaffMemberService> StaffServices { get; }
    DbSet<WorkingHours> WorkingHours { get; }
    DbSet<TimeOff> TimeOff { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Appointment> Appointments { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Usuario autenticado de la petición actual (null fuera de una petición HTTP).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}

/// <summary>
/// Negocio de la petición actual. En el panel sale de la base de datos al validar la sesión (nunca del token ni
/// de la petición). Null para el superadmin y para peticiones anónimas: en ese caso el filtro global no deja ver
/// ningún dato de negocio.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    /// <summary>Fija el negocio a mano. Solo para procesos sin petición HTTP (seed, pruebas).</summary>
    void Set(Guid? tenantId);
}

/// <summary>
/// Token del enlace privado de una cita (/cita/{token}). Se deriva del id de la cita con una clave secreta del
/// servidor, así se puede volver a generar para enviarlo por WhatsApp sin guardarlo: en la base solo va su hash.
/// </summary>
public interface IManageLinks
{
    string TokenFor(Guid appointmentId);
}

public static class TenantContextExtensions
{
    public static Guid RequireTenantId(this ITenantContext tenant) =>
        tenant.TenantId ?? throw new UnauthorizedAccessException("La sesión no pertenece a ningún negocio.");
}
