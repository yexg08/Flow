using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Team;

public record StaffDto(
    Guid Id, string Name, string Color, bool IsActive, IReadOnlyList<Guid> ServiceIds, IReadOnlyList<TimeRangeDto> WorkingHours);

public record StaffUpsertRequest(string Name, string Color, bool IsActive, IReadOnlyList<Guid>? ServiceIds);

public record ScheduleRequest(IReadOnlyList<TimeRangeDto> Ranges);

public class StaffUpsertRequestValidator : AbstractValidator<StaffUpsertRequest>
{
    public StaffUpsertRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(80).WithMessage("El nombre no puede superar 80 caracteres.");
        RuleFor(x => x.Color).HexColor();
        RuleFor(x => x.ServiceIds)
            .Must(ids => ids is null || ids.Count <= 200).WithMessage("Demasiados servicios.");
    }
}

public class ScheduleRequestValidator : AbstractValidator<ScheduleRequest>
{
    public ScheduleRequestValidator()
    {
        RuleFor(x => x.Ranges)
            .Must(r => r is null || r.Count <= 7 * Schedule.MaxRangesPerDay).WithMessage("El horario tiene demasiados tramos.")
            .Custom((ranges, context) =>
            {
                if (Schedule.Validate(ranges) is { } error) context.AddFailure(error);
            });
    }
}

public interface ITeamService
{
    Task<IReadOnlyList<StaffDto>> ListAsync(CancellationToken ct);
    Task<StaffDto> GetAsync(Guid id, CancellationToken ct);
    Task<StaffDto> CreateAsync(StaffUpsertRequest request, CancellationToken ct);
    Task<StaffDto> UpdateAsync(Guid id, StaffUpsertRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<StaffDto> SetScheduleAsync(Guid id, ScheduleRequest request, CancellationToken ct);
}

/// <summary>
/// El equipo del negocio de la sesión: quién atiende, qué servicios hace y en qué horario. Como en todo el panel,
/// el filtro global limita las consultas al negocio de la sesión (también en los Include).
/// </summary>
public class TeamService(IAppDbContext db) : ITeamService
{
    private const string Resource = "La persona";

    public async Task<IReadOnlyList<StaffDto>> ListAsync(CancellationToken ct)
    {
        var staff = await db.Staff.AsNoTracking()
            .Include(s => s.Services)
            .Include(s => s.WorkingHours)
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.Name)
            .ToListAsync(ct);
        return staff.Select(ToDto).ToList();
    }

    public async Task<StaffDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await FindAsync(id, ct));

    public async Task<StaffDto> CreateAsync(StaffUpsertRequest request, CancellationToken ct)
    {
        var staff = new StaffMember();
        Apply(staff, request);
        await SetServicesAsync(staff, request.ServiceIds, ct);
        db.Staff.Add(staff);
        await db.SaveChangesAsync(ct);
        return ToDto(staff);
    }

    public async Task<StaffDto> UpdateAsync(Guid id, StaffUpsertRequest request, CancellationToken ct)
    {
        var staff = await FindAsync(id, ct);
        Apply(staff, request);
        await SetServicesAsync(staff, request.ServiceIds, ct);
        await db.SaveChangesAsync(ct);
        return ToDto(staff);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var staff = await FindAsync(id, ct);
        if (await db.Appointments.AnyAsync(a => a.StaffMemberId == id, ct))
            throw new BusinessRuleException("Esta persona tiene citas. En vez de borrarla, edítala y marca que no recibe reservas.");
        db.Staff.Remove(staff);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Reemplaza el horario semanal completo de la persona.</summary>
    public async Task<StaffDto> SetScheduleAsync(Guid id, ScheduleRequest request, CancellationToken ct)
    {
        var staff = await FindAsync(id, ct);

        db.WorkingHours.RemoveRange(staff.WorkingHours);
        // Se agregan al DbSet y no a la navegación: traen su Id ya generado, y por la navegación EF los tomaría como
        // filas existentes (UPDATE) en vez de nuevas (INSERT).
        db.WorkingHours.AddRange(request.Ranges.Select(r =>
        {
            Schedule.TryParseTime(r.Start, out var start);
            Schedule.TryParseTime(r.End, out var end);
            return new WorkingHours { StaffMemberId = staff.Id, DayOfWeek = r.DayOfWeek, Start = start, End = end };
        }));

        await db.SaveChangesAsync(ct);
        return ToDto(staff);
    }

    private async Task<StaffMember> FindAsync(Guid id, CancellationToken ct) =>
        await db.Staff
            .Include(s => s.Services)
            .Include(s => s.WorkingHours)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException(Resource);

    private static void Apply(StaffMember staff, StaffUpsertRequest request)
    {
        staff.Name = request.Name.Trim();
        staff.Color = request.Color.ToLowerInvariant();
        staff.IsActive = request.IsActive;
    }

    /// <summary>
    /// Solo se pueden asignar servicios del propio negocio: los ids se buscan con el filtro global, así que un id de
    /// otro negocio no se encuentra y se rechaza igual que uno inventado.
    /// </summary>
    private async Task SetServicesAsync(StaffMember staff, IReadOnlyList<Guid>? serviceIds, CancellationToken ct)
    {
        var wanted = (serviceIds ?? []).Distinct().ToList();
        var found = await db.Services.Where(s => wanted.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        if (found.Count != wanted.Count)
            throw new FieldValidationException("ServiceIds", "Uno de los servicios elegidos no existe.");

        staff.Services.RemoveAll(link => !wanted.Contains(link.ServiceId));
        foreach (var serviceId in wanted.Where(id => staff.Services.All(link => link.ServiceId != id)))
            staff.Services.Add(new StaffMemberService { StaffMemberId = staff.Id, ServiceId = serviceId });
    }

    private static StaffDto ToDto(StaffMember s) => new(
        s.Id, s.Name, s.Color, s.IsActive,
        s.Services.Select(x => x.ServiceId).ToList(),
        s.WorkingHours
            .OrderBy(w => ((int)w.DayOfWeek + 6) % 7).ThenBy(w => w.Start)
            .Select(w => new TimeRangeDto(w.DayOfWeek, w.Start.ToString("HH:mm"), w.End.ToString("HH:mm")))
            .ToList());
}
