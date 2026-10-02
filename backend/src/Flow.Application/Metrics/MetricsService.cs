using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Metrics;

public record DailyPointDto(DateOnly Date, int Appointments, int Completed);

public record RankingDto(string Name, string? Color, int Appointments, decimal Revenue);

/// <param name="Appointments">Citas no canceladas del periodo.</param>
/// <param name="Revenue">Suma de las atendidas.</param>
/// <param name="NoShowRate">Inasistencias sobre las citas ya resueltas (atendidas + inasistencias). 0 si no hay.</param>
/// <param name="OnlineShare">Parte de las citas no canceladas que llegó por la página pública.</param>
public record MetricsDto(
    DateOnly From, DateOnly To, string Currency,
    int Appointments, int Completed, int NoShows, int Cancelled, decimal Revenue,
    double NoShowRate, double OnlineShare, int NewCustomers,
    int UpcomingAppointments, decimal UpcomingRevenue,
    IReadOnlyList<DailyPointDto> Daily, IReadOnlyList<RankingDto> TopServices, IReadOnlyList<RankingDto> ByStaff);

public interface IMetricsService
{
    /// <summary>Los últimos <paramref name="days"/> días hasta hoy (hora del negocio), más lo que viene.</summary>
    Task<MetricsDto> GetAsync(int days, CancellationToken ct);
}

/// <summary>Métricas del negocio de la sesión. El volumen de un negocio pequeño cabe en memoria: se agrupa aquí.</summary>
public class MetricsService(IAppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IMetricsService
{
    public async Task<MetricsDto> GetAsync(int days, CancellationToken ct)
    {
        days = Math.Clamp(days, 7, 365);
        var tenantId = tenant.RequireTenantId();
        var business = await db.Tenants.Where(t => t.Id == tenantId).Select(t => new { t.TimeZone, t.Currency }).FirstAsync(ct);
        var zone = BusinessTime.Zone(business.TimeZone);

        var to = BusinessTime.Today(zone, timeProvider);
        var from = to.AddDays(-(days - 1));
        var start = BusinessTime.StartOfDayUtc(from, zone);
        var end = BusinessTime.StartOfDayUtc(to.AddDays(1), zone);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var rows = await db.Appointments.AsNoTracking()
            .Where(a => a.StartsAtUtc >= start && a.StartsAtUtc < end)
            .Select(a => new { a.StartsAtUtc, a.Status, a.Source, a.Price, a.ServiceId, a.StaffMemberId })
            .ToListAsync(ct);
        var active = rows.Where(r => r.Status != AppointmentStatus.Cancelled).ToList();
        var completed = rows.Where(r => r.Status == AppointmentStatus.Completed).ToList();
        var noShows = rows.Count(r => r.Status == AppointmentStatus.NoShow);

        var upcoming = await db.Appointments.AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Confirmed && a.StartsAtUtc >= now)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Revenue = g.Sum(a => a.Price) })
            .FirstOrDefaultAsync(ct);

        var newCustomers = await db.Customers.CountAsync(c => c.CreatedAt >= start && c.CreatedAt < end, ct);

        var byDay = active.GroupBy(r => DateOnly.FromDateTime(BusinessTime.ToLocal(r.StartsAtUtc, zone))).ToDictionary(g => g.Key);
        var daily = Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .Select(d => byDay.TryGetValue(d, out var g)
                ? new DailyPointDto(d, g.Count(), g.Count(r => r.Status == AppointmentStatus.Completed))
                : new DailyPointDto(d, 0, 0))
            .ToList();

        var serviceNames = await db.Services.AsNoTracking().Select(s => new { s.Id, s.Name }).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var staffInfo = await db.Staff.AsNoTracking().Select(s => new { s.Id, s.Name, s.Color }).ToDictionaryAsync(s => s.Id, ct);

        var topServices = active.GroupBy(r => r.ServiceId)
            .Select(g => new RankingDto(serviceNames.GetValueOrDefault(g.Key, "Servicio"), null, g.Count(),
                g.Where(r => r.Status == AppointmentStatus.Completed).Sum(r => r.Price)))
            .OrderByDescending(r => r.Appointments).ThenByDescending(r => r.Revenue)
            .Take(5)
            .ToList();

        var byStaff = active.GroupBy(r => r.StaffMemberId)
            .Select(g => new RankingDto(
                staffInfo.TryGetValue(g.Key, out var s) ? s.Name : "Persona", staffInfo.TryGetValue(g.Key, out var c) ? c.Color : null,
                g.Count(), g.Where(r => r.Status == AppointmentStatus.Completed).Sum(r => r.Price)))
            .OrderByDescending(r => r.Appointments)
            .ToList();

        var resolved = completed.Count + noShows;
        return new MetricsDto(
            from, to, business.Currency,
            active.Count, completed.Count, noShows, rows.Count - active.Count, completed.Sum(r => r.Price),
            resolved == 0 ? 0 : Math.Round((double)noShows / resolved, 3),
            active.Count == 0 ? 0 : Math.Round((double)active.Count(r => r.Source == AppointmentSource.Online) / active.Count, 3),
            newCustomers,
            upcoming?.Count ?? 0, upcoming?.Revenue ?? 0,
            daily, topServices, byStaff);
    }
}
