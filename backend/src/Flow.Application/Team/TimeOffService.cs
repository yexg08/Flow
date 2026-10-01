using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Team;

/// <summary>Las fechas van y vienen en hora local del negocio, sin zona (p. ej. "2026-10-05T08:00").</summary>
public record TimeOffDto(Guid Id, Guid? StaffMemberId, string? StaffName, DateTime StartsAt, DateTime EndsAt, string? Reason);

/// <param name="StaffMemberId">Null = bloquea todo el negocio (p. ej. un festivo).</param>
public record TimeOffRequest(Guid? StaffMemberId, DateTime StartsAt, DateTime EndsAt, string? Reason);

public class TimeOffRequestValidator : AbstractValidator<TimeOffRequest>
{
    public TimeOffRequestValidator()
    {
        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt).WithMessage("El fin debe ser después del inicio.");
        RuleFor(x => x)
            .Must(x => x.EndsAt - x.StartsAt <= TimeSpan.FromDays(366))
            .OverridePropertyName("EndsAt").WithMessage("Un bloqueo no puede durar más de un año.");
        RuleFor(x => x.Reason)
            .MaximumLength(200).WithMessage("El motivo no puede superar 200 caracteres.");
    }
}

public interface ITimeOffService
{
    /// <summary>Bloqueos que aún no han terminado, del más próximo al más lejano.</summary>
    Task<IReadOnlyList<TimeOffDto>> ListUpcomingAsync(CancellationToken ct);
    Task<TimeOffDto> CreateAsync(TimeOffRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public class TimeOffService(IAppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : ITimeOffService
{
    public async Task<IReadOnlyList<TimeOffDto>> ListUpcomingAsync(CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var rows = await db.TimeOff.AsNoTracking()
            .Where(t => t.EndsAtUtc > now)
            .OrderBy(t => t.StartsAtUtc)
            .Select(t => new
            {
                t.Id, t.StaffMemberId, t.StartsAtUtc, t.EndsAtUtc, t.Reason,
                StaffName = db.Staff.Where(s => s.Id == t.StaffMemberId).Select(s => s.Name).FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(r => new TimeOffDto(
            r.Id, r.StaffMemberId, r.StaffName, ToLocal(r.StartsAtUtc, zone), ToLocal(r.EndsAtUtc, zone), r.Reason)).ToList();
    }

    public async Task<TimeOffDto> CreateAsync(TimeOffRequest request, CancellationToken ct)
    {
        string? staffName = null;
        if (request.StaffMemberId is { } staffId)
        {
            // Filtro global: una persona de otro negocio no se encuentra.
            staffName = await db.Staff.Where(s => s.Id == staffId).Select(s => s.Name).FirstOrDefaultAsync(ct)
                ?? throw new FieldValidationException(nameof(request.StaffMemberId), "Esa persona no existe en tu equipo.");
        }

        var zone = await ZoneAsync(ct);
        var timeOff = new TimeOff
        {
            StaffMemberId = request.StaffMemberId,
            StartsAtUtc = ToUtc(request.StartsAt, zone, nameof(request.StartsAt)),
            EndsAtUtc = ToUtc(request.EndsAt, zone, nameof(request.EndsAt)),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim()
        };
        db.TimeOff.Add(timeOff);
        await db.SaveChangesAsync(ct);

        return new TimeOffDto(timeOff.Id, timeOff.StaffMemberId, staffName,
            ToLocal(timeOff.StartsAtUtc, zone), ToLocal(timeOff.EndsAtUtc, zone), timeOff.Reason);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var timeOff = await db.TimeOff.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("El bloqueo");
        db.TimeOff.Remove(timeOff);
        await db.SaveChangesAsync(ct);
    }

    private async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var zoneId = await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.TimeZone).FirstAsync(ct);
        return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
    }

    /// <summary>Hora local del negocio → UTC. Rechaza horas que no existen (el salto del cambio de horario).</summary>
    private static DateTime ToUtc(DateTime local, TimeZoneInfo zone, string field)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
            throw new FieldValidationException(field, "Esa hora no existe por el cambio de horario. Elige otra.");
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }

    private static DateTime ToLocal(DateTime utc, TimeZoneInfo zone) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone), DateTimeKind.Unspecified);
}
