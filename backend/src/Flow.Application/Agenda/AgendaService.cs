using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Application.Team;
using Flow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Agenda;

/// <summary>Una cita en la agenda del panel. Fechas en hora local del negocio, sin zona.</summary>
public record AgendaAppointmentDto(
    Guid Id, DateTime StartsAt, DateTime EndsAt, AppointmentStatus Status,
    Guid ServiceId, string ServiceName, Guid StaffMemberId, string StaffName, string StaffColor,
    Guid CustomerId, string CustomerName, string CustomerPhone, string? CustomerNote, decimal Price);

public record AgendaDto(IReadOnlyList<AgendaAppointmentDto> Appointments, IReadOnlyList<TimeOffDto> TimeOff);

/// <summary>
/// Cita creada desde el panel (llamada, cliente que llega en persona). El cliente es uno existente (CustomerId) o uno
/// nuevo/reconocido por su celular (CustomerName + CustomerPhone).
/// </summary>
public record PanelBookingRequest(
    Guid ServiceId, Guid StaffMemberId, DateOnly Date, string Time,
    Guid? CustomerId, string? CustomerName, string? CustomerPhone, string? Note);

public record MoveAppointmentRequest(Guid StaffMemberId, DateOnly Date, string Time);

public record SetStatusRequest(AppointmentStatus Status);

public class PanelBookingRequestValidator : AbstractValidator<PanelBookingRequest>
{
    public PanelBookingRequestValidator()
    {
        RuleFor(x => x.Time).Must(t => Schedule.TryParseTime(t, out var time) && time.Minute % Schedule.StepMinutes == 0)
            .WithMessage("Elige una hora (de 5 en 5 minutos).");
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Escribe el nombre del cliente.")
            .MaximumLength(80).WithMessage("El nombre no puede superar 80 caracteres.")
            .When(x => x.CustomerId is null);
        RuleFor(x => x.CustomerPhone)
            .Must(p => Phones.Normalize(p) is not null).WithMessage("Escribe un celular válido.")
            .When(x => x.CustomerId is null);
        RuleFor(x => x.Note).MaximumLength(300).WithMessage("La nota no puede superar 300 caracteres.");
    }
}

public class MoveAppointmentRequestValidator : AbstractValidator<MoveAppointmentRequest>
{
    public MoveAppointmentRequestValidator()
    {
        RuleFor(x => x.Time).Must(t => Schedule.TryParseTime(t, out var time) && time.Minute % Schedule.StepMinutes == 0)
            .WithMessage("Elige una hora (de 5 en 5 minutos).");
    }
}

public class SetStatusRequestValidator : AbstractValidator<SetStatusRequest>
{
    public SetStatusRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage("El estado no es válido.");
    }
}

public interface IAgendaService
{
    Task<AgendaDto> GetAsync(DateOnly from, DateOnly to, CancellationToken ct);
    Task<AgendaAppointmentDto> CreateAsync(PanelBookingRequest request, CancellationToken ct);
    Task<AgendaAppointmentDto> MoveAsync(Guid id, MoveAppointmentRequest request, CancellationToken ct);
    Task<AgendaAppointmentDto> SetStatusAsync(Guid id, SetStatusRequest request, CancellationToken ct);

    /// <summary>Todas las citas de un cliente, de la más reciente a la más antigua.</summary>
    Task<IReadOnlyList<AgendaAppointmentDto>> GetCustomerHistoryAsync(Guid customerId, CancellationToken ct);
}

/// <summary>
/// Agenda del negocio de la sesión. A diferencia de la página pública, el negocio puede agendar fuera del horario
/// del equipo (es su decisión), pero nunca encima de otra cita de la misma persona ni durante un bloqueo.
/// </summary>
public class AgendaService(IAppDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IAgendaService
{
    public const int MaxRangeDays = 42;

    public async Task<AgendaDto> GetAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from || to.DayNumber - from.DayNumber > MaxRangeDays)
            throw new FieldValidationException("to", $"El rango debe ser de máximo {MaxRangeDays} días.");

        var zone = await ZoneAsync(ct);
        var start = BusinessTime.StartOfDayUtc(from, zone);
        var end = BusinessTime.StartOfDayUtc(to.AddDays(1), zone);

        var appointments = await Project(db.Appointments.Where(a => a.StartsAtUtc < end && a.EndsAtUtc > start), zone, ct);

        var timeOff = await db.TimeOff.AsNoTracking()
            .Where(t => t.StartsAtUtc < end && t.EndsAtUtc > start)
            .OrderBy(t => t.StartsAtUtc)
            .Select(t => new
            {
                t.Id, t.StaffMemberId, t.StartsAtUtc, t.EndsAtUtc, t.Reason,
                StaffName = db.Staff.Where(s => s.Id == t.StaffMemberId).Select(s => s.Name).FirstOrDefault()
            })
            .ToListAsync(ct);

        return new AgendaDto(appointments, timeOff.Select(t => new TimeOffDto(
            t.Id, t.StaffMemberId, t.StaffName, BusinessTime.ToLocal(t.StartsAtUtc, zone), BusinessTime.ToLocal(t.EndsAtUtc, zone), t.Reason)).ToList());
    }

    public async Task<AgendaAppointmentDto> CreateAsync(PanelBookingRequest request, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceId, ct)
            ?? throw new FieldValidationException(nameof(request.ServiceId), "Ese servicio no existe.");
        await EnsureStaffAsync(request.StaffMemberId, nameof(request.StaffMemberId), ct);
        var (startUtc, endUtc) = ToUtcRange(request.Date, request.Time, service.DurationMinutes, zone);
        EnsureWithinRange(request.Date, zone);
        await EnsureFreeAsync(request.StaffMemberId, startUtc, endUtc, ignoreId: null, ct);

        Customer customer;
        if (request.CustomerId is { } customerId)
        {
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct)
                ?? throw new FieldValidationException(nameof(request.CustomerId), "Ese cliente no existe.");
        }
        else
        {
            var phone = Phones.Normalize(request.CustomerPhone)!;
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct) ?? AddCustomer(phone);
            customer.Name = request.CustomerName!.Trim();
        }

        var appointment = new Appointment
        {
            CustomerId = customer.Id,
            ServiceId = service.Id,
            StaffMemberId = request.StaffMemberId,
            StartsAtUtc = startUtc,
            EndsAtUtc = endUtc,
            Price = service.Price,
            CustomerNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            // Toda cita tiene token. Este no se muestra a nadie: el cliente lo recibirá cuando haya avisos (Fase 5).
            ManageTokenHash = SecureTokens.Hash(SecureTokens.Generate())
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(appointment.Id, zone, ct);
    }

    public async Task<AgendaAppointmentDto> MoveAsync(Guid id, MoveAppointmentRequest request, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var appointment = await FindAsync(id, ct);
        if (appointment.Status != AppointmentStatus.Confirmed)
            throw new BusinessRuleException("Solo se puede mover una cita confirmada.");

        await EnsureStaffAsync(request.StaffMemberId, nameof(request.StaffMemberId), ct);
        var duration = (int)(appointment.EndsAtUtc - appointment.StartsAtUtc).TotalMinutes;
        var (startUtc, endUtc) = ToUtcRange(request.Date, request.Time, duration, zone);
        EnsureWithinRange(request.Date, zone);
        await EnsureFreeAsync(request.StaffMemberId, startUtc, endUtc, ignoreId: appointment.Id, ct);

        appointment.StaffMemberId = request.StaffMemberId;
        appointment.StartsAtUtc = startUtc;
        appointment.EndsAtUtc = endUtc;
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(appointment.Id, zone, ct);
    }

    /// <summary>
    /// Confirmada → atendida o no asistió (solo cuando ya empezó), o cancelada. Cualquier estado → confirmada sirve
    /// para deshacer; reactivar una cancelada exige que el horario siga libre (lo garantiza la base).
    /// </summary>
    public async Task<AgendaAppointmentDto> SetStatusAsync(Guid id, SetStatusRequest request, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var appointment = await FindAsync(id, ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var next = request.Status;

        if (next == appointment.Status) return await GetDtoAsync(id, zone, ct);

        switch (next)
        {
            case AppointmentStatus.Completed or AppointmentStatus.NoShow when appointment.StartsAtUtc > now:
                throw new BusinessRuleException("Una cita que aún no empieza no se puede marcar como atendida o como inasistencia.");
            case AppointmentStatus.Completed or AppointmentStatus.NoShow when appointment.Status == AppointmentStatus.Cancelled:
                throw new BusinessRuleException("La cita está cancelada. Reactívala primero.");
            case AppointmentStatus.Confirmed when appointment.Status == AppointmentStatus.Cancelled:
                if (appointment.EndsAtUtc <= now) throw new BusinessRuleException("Esa cita ya pasó: no se puede reactivar.");
                await EnsureFreeAsync(appointment.StaffMemberId, appointment.StartsAtUtc, appointment.EndsAtUtc, appointment.Id, ct);
                appointment.CancelledAt = null;
                break;
            case AppointmentStatus.Cancelled:
                appointment.CancelledAt = now;
                break;
        }

        appointment.Status = next;
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(id, zone, ct);
    }

    public async Task<IReadOnlyList<AgendaAppointmentDto>> GetCustomerHistoryAsync(Guid customerId, CancellationToken ct)
    {
        var history = await Project(db.Appointments.Where(a => a.CustomerId == customerId), await ZoneAsync(ct), ct);
        history.Reverse();
        return history;
    }

    private Customer AddCustomer(string phone)
    {
        var customer = new Customer { Phone = phone };
        db.Customers.Add(customer);
        return customer;
    }

    private async Task<Appointment> FindAsync(Guid id, CancellationToken ct) =>
        await db.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("La cita");

    private async Task EnsureStaffAsync(Guid staffId, string field, CancellationToken ct)
    {
        if (!await db.Staff.AnyAsync(s => s.Id == staffId && s.IsActive, ct))
            throw new FieldValidationException(field, "Esa persona no existe o no está recibiendo citas.");
    }

    /// <summary>
    /// Revisa antes de guardar para dar un mensaje claro. La garantía real contra cruces entre citas es la restricción
    /// de exclusión de la base (por si dos personas del negocio agendan a la vez).
    /// </summary>
    private async Task EnsureFreeAsync(Guid staffId, DateTime startUtc, DateTime endUtc, Guid? ignoreId, CancellationToken ct)
    {
        var overlapsAppointment = await db.Appointments.AnyAsync(a =>
            a.StaffMemberId == staffId && a.Status != AppointmentStatus.Cancelled && (ignoreId == null || a.Id != ignoreId)
            && a.StartsAtUtc < endUtc && a.EndsAtUtc > startUtc, ct);
        if (overlapsAppointment) throw new BusinessRuleException("Esa persona ya tiene una cita en ese horario.");

        var overlapsTimeOff = await db.TimeOff.AnyAsync(t =>
            (t.StaffMemberId == null || t.StaffMemberId == staffId) && t.StartsAtUtc < endUtc && t.EndsAtUtc > startUtc, ct);
        if (overlapsTimeOff) throw new BusinessRuleException("Ese horario está bloqueado (vacaciones, festivo...).");
    }

    private static (DateTime Start, DateTime End) ToUtcRange(DateOnly date, string time, int minutes, TimeZoneInfo zone)
    {
        Schedule.TryParseTime(time, out var localTime);
        if (!BusinessTime.TryToUtc(date.ToDateTime(localTime), zone, out var startUtc))
            throw new FieldValidationException("Time", "Esa hora no existe por el cambio de horario. Elige otra.");
        return (startUtc, startUtc.AddMinutes(minutes));
    }

    /// <summary>El panel puede registrar citas pasadas recientes (alguien que llegó sin reservar), pero con límites.</summary>
    private void EnsureWithinRange(DateOnly date, TimeZoneInfo zone)
    {
        var today = BusinessTime.Today(zone, timeProvider);
        if (date < today.AddDays(-30) || date > today.AddDays(365))
            throw new FieldValidationException("Date", "La fecha debe estar entre hace 30 días y dentro de un año.");
    }

    private async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        return BusinessTime.Zone(await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.TimeZone).FirstAsync(ct));
    }

    private async Task<AgendaAppointmentDto> GetDtoAsync(Guid id, TimeZoneInfo zone, CancellationToken ct) =>
        (await Project(db.Appointments.Where(a => a.Id == id), zone, ct)).Single();

    private async Task<List<AgendaAppointmentDto>> Project(IQueryable<Appointment> query, TimeZoneInfo zone, CancellationToken ct)
    {
        var rows = await query.AsNoTracking()
            .OrderBy(a => a.StartsAtUtc)
            .Select(a => new
            {
                a.Id, a.StartsAtUtc, a.EndsAtUtc, a.Status, a.ServiceId, a.StaffMemberId, a.CustomerId, a.CustomerNote, a.Price,
                ServiceName = db.Services.Where(s => s.Id == a.ServiceId).Select(s => s.Name).First(),
                Staff = db.Staff.Where(s => s.Id == a.StaffMemberId).Select(s => new { s.Name, s.Color }).First(),
                Customer = db.Customers.Where(c => c.Id == a.CustomerId).Select(c => new { c.Name, c.Phone }).First()
            })
            .ToListAsync(ct);

        return rows.Select(r => new AgendaAppointmentDto(
            r.Id, BusinessTime.ToLocal(r.StartsAtUtc, zone), BusinessTime.ToLocal(r.EndsAtUtc, zone), r.Status,
            r.ServiceId, r.ServiceName, r.StaffMemberId, r.Staff.Name, r.Staff.Color,
            r.CustomerId, r.Customer.Name, r.Customer.Phone, r.CustomerNote, r.Price)).ToList();
    }
}
