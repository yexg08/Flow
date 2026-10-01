using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Booking;

/// <summary>Una cita vista desde el panel del negocio. Fechas en hora local del negocio, sin zona.</summary>
public record UpcomingAppointmentDto(
    Guid Id, DateTime StartsAt, DateTime EndsAt, string ServiceName,
    Guid StaffMemberId, string StaffName, string StaffColor,
    string CustomerName, string CustomerPhone, string? CustomerNote, decimal Price);

public interface IAppointmentsService
{
    Task<IReadOnlyList<UpcomingAppointmentDto>> ListUpcomingAsync(int limit, CancellationToken ct);
}

/// <summary>Citas del negocio de la sesión (el filtro global limita todo a ese negocio).</summary>
public class AppointmentsService(IAppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IAppointmentsService
{
    public async Task<IReadOnlyList<UpcomingAppointmentDto>> ListUpcomingAsync(int limit, CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var zone = BusinessTime.Zone(await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.TimeZone).FirstAsync(ct));
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var rows = await db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Confirmed && a.EndsAtUtc > now)
            .OrderBy(a => a.StartsAtUtc)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(a => new
            {
                a.Id, a.StartsAtUtc, a.EndsAtUtc, a.StaffMemberId, a.CustomerNote, a.Price,
                ServiceName = db.Services.Where(s => s.Id == a.ServiceId).Select(s => s.Name).First(),
                Staff = db.Staff.Where(s => s.Id == a.StaffMemberId).Select(s => new { s.Name, s.Color }).First(),
                Customer = db.Customers.Where(c => c.Id == a.CustomerId).Select(c => new { c.Name, c.Phone }).First()
            })
            .ToListAsync(ct);

        return rows.Select(r => new UpcomingAppointmentDto(
            r.Id, BusinessTime.ToLocal(r.StartsAtUtc, zone), BusinessTime.ToLocal(r.EndsAtUtc, zone), r.ServiceName,
            r.StaffMemberId, r.Staff.Name, r.Staff.Color, r.Customer.Name, r.Customer.Phone, r.CustomerNote, r.Price)).ToList();
    }
}
