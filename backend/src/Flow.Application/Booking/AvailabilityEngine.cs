using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Booking;

/// <summary>Un horario libre de una persona: hora local de inicio y el rango exacto en UTC.</summary>
public record FreeSlot(Guid StaffId, TimeOnly LocalStart, DateTime StartUtc, DateTime EndUtc);

/// <summary>
/// Calcula los horarios libres de un día para un servicio. Un horario está libre si:
/// - la persona está activa, hace el servicio y ese día tiene un tramo de horario donde cabe la duración completa;
/// - no se cruza con otra cita activa suya ni con un bloqueo suyo o de todo el negocio;
/// - empieza al menos <see cref="LeadMinutes"/> minutos en el futuro.
/// Trabaja sobre el negocio de la sesión (filtro global): el llamador debe haberlo fijado.
/// </summary>
public class AvailabilityEngine(IAppDbContext db, TimeProvider timeProvider)
{
    /// <summary>Cada cuánto se ofrece un horario dentro de un tramo (8:00, 8:15, 8:30...).</summary>
    public const int StepMinutes = 15;

    /// <summary>Anticipación mínima para reservar.</summary>
    public const int LeadMinutes = 15;

    /// <summary>Hasta cuántos días adelante se puede reservar.</summary>
    public const int WindowDays = 60;

    public async Task<List<FreeSlot>> FindAsync(
        TimeZoneInfo zone, Guid serviceId, int durationMinutes, DateOnly date, Guid? staffId, Guid? ignoreAppointmentId,
        CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking()
            .Include(s => s.WorkingHours)
            .Where(s => s.IsActive && s.Services.Any(x => x.ServiceId == serviceId) && (staffId == null || s.Id == staffId))
            .ToListAsync(ct);
        if (staff.Count == 0) return [];

        var staffIds = staff.Select(s => s.Id).ToList();
        var dayStart = BusinessTime.StartOfDayUtc(date, zone);
        var dayEnd = BusinessTime.StartOfDayUtc(date.AddDays(1), zone);

        var busy = await db.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelled
                && staffIds.Contains(a.StaffMemberId)
                && a.StartsAtUtc < dayEnd && a.EndsAtUtc > dayStart
                && (ignoreAppointmentId == null || a.Id != ignoreAppointmentId))
            .Select(a => new { a.StaffMemberId, a.StartsAtUtc, a.EndsAtUtc })
            .ToListAsync(ct);

        var timeOff = await db.TimeOff.AsNoTracking()
            .Where(t => (t.StaffMemberId == null || staffIds.Contains(t.StaffMemberId.Value))
                && t.StartsAtUtc < dayEnd && t.EndsAtUtc > dayStart)
            .Select(t => new { t.StaffMemberId, t.StartsAtUtc, t.EndsAtUtc })
            .ToListAsync(ct);

        var earliest = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(LeadMinutes);
        var slots = new List<FreeSlot>();

        foreach (var person in staff)
        {
            foreach (var range in person.WorkingHours.Where(w => w.DayOfWeek == date.DayOfWeek).OrderBy(w => w.Start))
            {
                var rangeEnd = range.End.Hour * 60 + range.End.Minute;
                for (var minute = range.Start.Hour * 60 + range.Start.Minute; minute + durationMinutes <= rangeEnd; minute += StepMinutes)
                {
                    var localStart = new TimeOnly(minute / 60, minute % 60);
                    if (!BusinessTime.TryToUtc(date.ToDateTime(localStart), zone, out var startUtc)) continue;
                    var endUtc = startUtc.AddMinutes(durationMinutes);

                    if (startUtc < earliest) continue;
                    if (busy.Any(b => b.StaffMemberId == person.Id && b.StartsAtUtc < endUtc && b.EndsAtUtc > startUtc)) continue;
                    if (timeOff.Any(t => (t.StaffMemberId == null || t.StaffMemberId == person.Id)
                        && t.StartsAtUtc < endUtc && t.EndsAtUtc > startUtc)) continue;

                    slots.Add(new FreeSlot(person.Id, localStart, startUtc, endUtc));
                }
            }
        }

        return slots;
    }

    /// <summary>La fecha está entre hoy y dentro de <see cref="WindowDays"/> días (hora del negocio).</summary>
    public bool IsWithinWindow(DateOnly date, TimeZoneInfo zone)
    {
        var today = BusinessTime.Today(zone, timeProvider);
        return date >= today && date <= today.AddDays(WindowDays);
    }
}
