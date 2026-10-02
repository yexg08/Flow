using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Application.Team;
using Flow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Booking;

public record PublicSlotDto(string Time, IReadOnlyList<Guid> StaffIds);

public record PublicAvailabilityDto(DateOnly Date, IReadOnlyList<PublicSlotDto> Slots);

/// <param name="StaffMemberId">Null = "cualquier persona": se asigna quien tenga menos citas ese día.</param>
/// <param name="Time">Hora local del negocio, "HH:mm".</param>
public record BookingRequest(
    Guid ServiceId, Guid? StaffMemberId, DateOnly Date, string Time,
    string CustomerName, string CustomerPhone, string? CustomerEmail, string? Note, bool AcceptsPrivacy);

public record RescheduleRequest(DateOnly Date, string Time);

/// <summary>Lo que ve el cliente de su cita. Fechas en hora local del negocio, sin zona.</summary>
public record PublicAppointmentDto(
    string BusinessName, string BusinessSlug, string AccentColor, string? BusinessWhatsApp,
    Guid ServiceId, string ServiceName, int DurationMinutes, decimal Price, string Currency,
    Guid StaffMemberId, string StaffName, string StaffColor,
    DateTime StartsAt, DateTime EndsAt, string Status, bool CanChange, string CustomerName);

/// <summary>El token va UNA sola vez en esta respuesta: el cliente lo guarda en su enlace.</summary>
public record BookingResultDto(string ManageToken, PublicAppointmentDto Appointment);

public class BookingRequestValidator : AbstractValidator<BookingRequest>
{
    public BookingRequestValidator()
    {
        RuleFor(x => x.Time)
            .Must(t => Schedule.TryParseTime(t, out _)).WithMessage("Elige una hora.");
        RuleFor(x => x.CustomerName)
            .NotEmpty().WithMessage("Escribe tu nombre.")
            .MinimumLength(2).WithMessage("Escribe tu nombre.")
            .MaximumLength(80).WithMessage("El nombre no puede superar 80 caracteres.");
        RuleFor(x => x.CustomerPhone)
            .Must(p => Phones.Normalize(p) is not null).WithMessage("Escribe un celular válido (solo números).");
        RuleFor(x => x.CustomerEmail)
            .EmailAddress().WithMessage("El correo no es válido.")
            .MaximumLength(256).WithMessage("El correo es demasiado largo.")
            .When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));
        RuleFor(x => x.Note)
            .MaximumLength(300).WithMessage("La nota no puede superar 300 caracteres.");
        RuleFor(x => x.AcceptsPrivacy)
            .Equal(true).WithMessage("Para reservar debes autorizar el uso de tus datos.");
    }
}

public class RescheduleRequestValidator : AbstractValidator<RescheduleRequest>
{
    public RescheduleRequestValidator()
    {
        RuleFor(x => x.Time).Must(t => Schedule.TryParseTime(t, out _)).WithMessage("Elige una hora.");
    }
}

public interface IPublicBookingService
{
    Task<PublicAvailabilityDto> GetAvailabilityAsync(string slug, Guid serviceId, DateOnly date, Guid? staffId, CancellationToken ct);
    Task<BookingResultDto> BookAsync(string slug, BookingRequest request, CancellationToken ct);
    Task<PublicAppointmentDto> GetByTokenAsync(string token, CancellationToken ct);
    Task<PublicAvailabilityDto> GetRescheduleAvailabilityAsync(string token, DateOnly date, CancellationToken ct);
    Task<PublicAppointmentDto> RescheduleAsync(string token, RescheduleRequest request, CancellationToken ct);
    Task<PublicAppointmentDto> CancelAsync(string token, CancellationToken ct);
}

/// <summary>
/// Reservas desde la página pública, sin sesión. El negocio sale del enlace (slug) o del token de la cita, y se fija
/// como negocio de la petición: desde ahí el filtro global y el interceptor aplican igual que en el panel, así que un
/// id de servicio o de persona de otro negocio simplemente no se encuentra.
/// </summary>
public class PublicBookingService(
    IAppDbContext db, ITenantContext tenantContext, AvailabilityEngine engine, IManageLinks links, TimeProvider timeProvider)
    : IPublicBookingService
{
    /// <summary>Freno contra abusos: citas próximas que puede tener un mismo celular en un negocio.</summary>
    public const int MaxUpcomingPerCustomer = 3;

    private const string NotAvailable = "Esta cita ya no se puede cambiar. Si necesitas ayuda, escríbele al negocio.";

    public async Task<PublicAvailabilityDto> GetAvailabilityAsync(
        string slug, Guid serviceId, DateOnly date, Guid? staffId, CancellationToken ct)
    {
        var (_, zone) = await EnterBusinessAsync(slug, ct);
        if (!engine.IsWithinWindow(date, zone)) return new PublicAvailabilityDto(date, []);

        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId && s.IsActive, ct)
            ?? throw new NotFoundException("El servicio");

        var slots = await engine.FindAsync(zone, service.Id, service.DurationMinutes, date, staffId, null, ct);
        return new PublicAvailabilityDto(date, Group(slots));
    }

    public async Task<BookingResultDto> BookAsync(string slug, BookingRequest request, CancellationToken ct)
    {
        var (_, zone) = await EnterBusinessAsync(slug, ct);
        if (!engine.IsWithinWindow(request.Date, zone))
            throw new FieldValidationException(nameof(request.Date), $"Puedes reservar desde hoy y hasta {AvailabilityEngine.WindowDays} días adelante.");

        var service = await db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.IsActive, ct)
            ?? throw new FieldValidationException(nameof(request.ServiceId), "Ese servicio no existe.");

        if (request.StaffMemberId is { } staffId && !await db.Staff.AnyAsync(s => s.Id == staffId && s.IsActive, ct))
            throw new FieldValidationException(nameof(request.StaffMemberId), "Esa persona no existe en este negocio.");

        // El horario se vuelve a calcular aquí: nunca se confía en lo que muestre o mande el navegador.
        Schedule.TryParseTime(request.Time, out var time);
        var candidates = (await engine.FindAsync(zone, service.Id, service.DurationMinutes, request.Date, request.StaffMemberId, null, ct))
            .Where(s => s.LocalStart == time)
            .ToList();
        if (candidates.Count == 0) throw new ScheduleConflictException();

        var slot = candidates.Count == 1 ? candidates[0] : await LeastBusyAsync(candidates, zone, request.Date, ct);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var phone = Phones.Normalize(request.CustomerPhone)!;
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct);
        if (customer is null)
        {
            customer = new Customer { Phone = phone };
            db.Customers.Add(customer);
        }
        else
        {
            var upcoming = await db.Appointments.CountAsync(a =>
                a.CustomerId == customer.Id && a.Status == AppointmentStatus.Confirmed && a.StartsAtUtc > now, ct);
            if (upcoming >= MaxUpcomingPerCustomer)
                throw new BusinessRuleException(
                    $"Ya tienes {MaxUpcomingPerCustomer} citas próximas en este negocio. Cancela una o escríbele al negocio.");
        }

        customer.Name = request.CustomerName.Trim();
        if (!string.IsNullOrWhiteSpace(request.CustomerEmail)) customer.Email = request.CustomerEmail.Trim();
        customer.PrivacyConsentAt = now;

        var appointment = new Appointment
        {
            CustomerId = customer.Id,
            ServiceId = service.Id,
            StaffMemberId = slot.StaffId,
            StartsAtUtc = slot.StartUtc,
            EndsAtUtc = slot.EndUtc,
            Price = service.Price,
            Source = AppointmentSource.Online,
            CustomerNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        };
        var token = links.TokenFor(appointment.Id);
        appointment.ManageTokenHash = SecureTokens.Hash(token);
        db.Appointments.Add(appointment);

        // Si otra reserva tomó el mismo horario entre el cálculo y este guardado, la restricción de exclusión de
        // PostgreSQL la rechaza y se traduce a ScheduleConflictException (409).
        await db.SaveChangesAsync(ct);

        return new BookingResultDto(token, await ToDtoAsync(appointment, zone, ct));
    }

    public async Task<PublicAppointmentDto> GetByTokenAsync(string token, CancellationToken ct)
    {
        var (appointment, zone) = await EnterByTokenAsync(token, tracked: false, ct);
        return await ToDtoAsync(appointment, zone, ct);
    }

    public async Task<PublicAvailabilityDto> GetRescheduleAvailabilityAsync(string token, DateOnly date, CancellationToken ct)
    {
        var (appointment, zone) = await EnterByTokenAsync(token, tracked: false, ct);
        if (!CanChange(appointment) || !engine.IsWithinWindow(date, zone)) return new PublicAvailabilityDto(date, []);

        var duration = (int)(appointment.EndsAtUtc - appointment.StartsAtUtc).TotalMinutes;
        var slots = await engine.FindAsync(zone, appointment.ServiceId, duration, date, appointment.StaffMemberId, appointment.Id, ct);
        return new PublicAvailabilityDto(date, Group(slots));
    }

    /// <summary>Mueve la cita a otro horario de la misma persona y el mismo servicio.</summary>
    public async Task<PublicAppointmentDto> RescheduleAsync(string token, RescheduleRequest request, CancellationToken ct)
    {
        var (appointment, zone) = await EnterByTokenAsync(token, tracked: true, ct);
        if (!CanChange(appointment)) throw new BusinessRuleException(NotAvailable);
        if (!engine.IsWithinWindow(request.Date, zone))
            throw new FieldValidationException(nameof(request.Date), $"Puedes elegir desde hoy y hasta {AvailabilityEngine.WindowDays} días adelante.");

        Schedule.TryParseTime(request.Time, out var time);
        var duration = (int)(appointment.EndsAtUtc - appointment.StartsAtUtc).TotalMinutes;
        var slot = (await engine.FindAsync(zone, appointment.ServiceId, duration, request.Date, appointment.StaffMemberId, appointment.Id, ct))
            .FirstOrDefault(s => s.LocalStart == time)
            ?? throw new ScheduleConflictException();

        appointment.StartsAtUtc = slot.StartUtc;
        appointment.EndsAtUtc = slot.EndUtc;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(appointment, zone, ct);
    }

    public async Task<PublicAppointmentDto> CancelAsync(string token, CancellationToken ct)
    {
        var (appointment, zone) = await EnterByTokenAsync(token, tracked: true, ct);
        if (!CanChange(appointment)) throw new BusinessRuleException(NotAvailable);

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(appointment, zone, ct);
    }

    /// <summary>Busca el negocio activo del enlace y lo fija como negocio de la petición.</summary>
    private async Task<(Guid TenantId, TimeZoneInfo Zone)> EnterBusinessAsync(string slug, CancellationToken ct)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Slug == normalized && t.IsActive)
            .Select(t => new { t.Id, t.TimeZone })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("El negocio");

        tenantContext.Set(tenant.Id);
        return (tenant.Id, BusinessTime.Zone(tenant.TimeZone));
    }

    /// <summary>
    /// Busca la cita por el hash de su token. Es la única consulta que ignora el filtro de negocio, porque el token es
    /// lo que dice de qué negocio es; luego ese negocio se fija como el de la petición.
    /// </summary>
    private async Task<(Appointment Appointment, TimeZoneInfo Zone)> EnterByTokenAsync(string token, bool tracked, CancellationToken ct)
    {
        var hash = SecureTokens.Hash(token ?? string.Empty);
        var tenantId = await db.Appointments.IgnoreQueryFilters()
            .Where(a => a.ManageTokenHash == hash)
            .Select(a => (Guid?)a.TenantId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("La cita");

        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId && t.IsActive)
            .Select(t => new { t.TimeZone })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("La cita");

        tenantContext.Set(tenantId);
        var query = tracked ? db.Appointments : db.Appointments.AsNoTracking();
        var appointment = await query.FirstAsync(a => a.ManageTokenHash == hash, ct);
        return (appointment, BusinessTime.Zone(tenant.TimeZone));
    }

    private bool CanChange(Appointment a) =>
        a.Status == AppointmentStatus.Confirmed && a.StartsAtUtc > timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Entre varias personas libres a la misma hora, la que tenga menos citas ese día (reparte el trabajo).</summary>
    private async Task<FreeSlot> LeastBusyAsync(List<FreeSlot> candidates, TimeZoneInfo zone, DateOnly date, CancellationToken ct)
    {
        var dayStart = BusinessTime.StartOfDayUtc(date, zone);
        var dayEnd = BusinessTime.StartOfDayUtc(date.AddDays(1), zone);
        var ids = candidates.Select(c => c.StaffId).ToList();
        var counts = await db.Appointments
            .Where(a => ids.Contains(a.StaffMemberId) && a.Status != AppointmentStatus.Cancelled
                && a.StartsAtUtc >= dayStart && a.StartsAtUtc < dayEnd)
            .GroupBy(a => a.StaffMemberId)
            .Select(g => new { StaffId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StaffId, x => x.Count, ct);

        return candidates.OrderBy(c => counts.GetValueOrDefault(c.StaffId)).ThenBy(c => c.StaffId).First();
    }

    private static List<PublicSlotDto> Group(List<FreeSlot> slots) =>
        slots.GroupBy(s => s.LocalStart)
            .OrderBy(g => g.Key)
            .Select(g => new PublicSlotDto(g.Key.ToString("HH:mm"), g.Select(s => s.StaffId).Distinct().ToList()))
            .ToList();

    private async Task<PublicAppointmentDto> ToDtoAsync(Appointment a, TimeZoneInfo zone, CancellationToken ct)
    {
        var info = await db.Appointments.AsNoTracking()
            .Where(x => x.Id == a.Id)
            .Select(x => new
            {
                Business = db.Tenants.Where(t => t.Id == x.TenantId)
                    .Select(t => new { t.Name, t.Slug, t.AccentColor, t.WhatsApp, t.Currency }).First(),
                Service = db.Services.Where(s => s.Id == x.ServiceId).Select(s => new { s.Name }).First(),
                Staff = db.Staff.Where(s => s.Id == x.StaffMemberId).Select(s => new { s.Name, s.Color }).First(),
                Customer = db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.Name).First()
            })
            .FirstAsync(ct);

        return new PublicAppointmentDto(
            info.Business.Name, info.Business.Slug, info.Business.AccentColor, info.Business.WhatsApp,
            a.ServiceId, info.Service.Name, (int)(a.EndsAtUtc - a.StartsAtUtc).TotalMinutes, a.Price, info.Business.Currency,
            a.StaffMemberId, info.Staff.Name, info.Staff.Color,
            BusinessTime.ToLocal(a.StartsAtUtc, zone), BusinessTime.ToLocal(a.EndsAtUtc, zone),
            a.Status.ToString(), CanChange(a), info.Customer);
    }
}
