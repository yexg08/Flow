using Flow.Application.Abstractions;
using Flow.Application.Agenda;
using Flow.Application.Common;
using Flow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Customers;

/// <summary>Fechas en hora local del negocio, sin zona.</summary>
public record CustomerSummaryDto(
    Guid Id, string Name, string Phone, string? Email, int Visits, int NoShows, DateTime? LastVisitAt, DateTime? NextAppointmentAt);

public record CustomerDetailDto(
    Guid Id, string Name, string Phone, string? Email, string? Notes, DateTime CreatedAt, bool ConsentedOnline,
    int Visits, int NoShows, IReadOnlyList<AgendaAppointmentDto> Appointments);

public record UpdateCustomerRequest(string Name, string? Email, string? Notes);

public class UpdateCustomerRequestValidator : AbstractValidator<UpdateCustomerRequest>
{
    public UpdateCustomerRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(80).WithMessage("El nombre no puede superar 80 caracteres.");
        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo no es válido.")
            .MaximumLength(256).WithMessage("El correo es demasiado largo.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Notes).MaximumLength(500).WithMessage("Las notas no pueden superar 500 caracteres.");
    }
}

public interface ICustomersService
{
    Task<IReadOnlyList<CustomerSummaryDto>> ListAsync(string? search, CancellationToken ct);
    Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken ct);
    Task<CustomerDetailDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct);
}

/// <summary>Clientes del negocio de la sesión (el filtro global limita todo a ese negocio).</summary>
public class CustomersService(IAppDbContext db, ITenantContext tenant, IAgendaService agenda, TimeProvider timeProvider) : ICustomersService
{
    private const int MaxResults = 200;

    public async Task<IReadOnlyList<CustomerSummaryDto>> ListAsync(string? search, CancellationToken ct)
    {
        var zone = await ZoneAsync(ct);
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            var digits = new string(term.Where(char.IsAsciiDigit).ToArray());
            // Contains se traduce a LIKE con los comodines escapados: "%" o "_" en la búsqueda se tratan como texto.
            query = query.Where(c => c.Name.ToLower().Contains(term) || (digits.Length >= 3 && c.Phone.Contains(digits)));
        }

        var rows = await query
            .Select(c => new
            {
                c.Id, c.Name, c.Phone, c.Email,
                Visits = db.Appointments.Count(a => a.CustomerId == c.Id && a.Status == AppointmentStatus.Completed),
                NoShows = db.Appointments.Count(a => a.CustomerId == c.Id && a.Status == AppointmentStatus.NoShow),
                LastVisit = db.Appointments
                    .Where(a => a.CustomerId == c.Id && a.Status == AppointmentStatus.Completed)
                    .Max(a => (DateTime?)a.StartsAtUtc),
                Next = db.Appointments
                    .Where(a => a.CustomerId == c.Id && a.Status == AppointmentStatus.Confirmed && a.StartsAtUtc > now)
                    .Min(a => (DateTime?)a.StartsAtUtc)
            })
            .OrderByDescending(c => c.Next != null).ThenBy(c => c.Name)
            .Take(MaxResults)
            .ToListAsync(ct);

        return rows.Select(r => new CustomerSummaryDto(
            r.Id, r.Name, r.Phone, r.Email, r.Visits, r.NoShows,
            r.LastVisit is { } last ? BusinessTime.ToLocal(last, zone) : null,
            r.Next is { } next ? BusinessTime.ToLocal(next, zone) : null)).ToList();
    }

    public async Task<CustomerDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("El cliente");
        return await ToDetailAsync(customer, ct);
    }

    public async Task<CustomerDetailDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("El cliente");
        customer.Name = request.Name.Trim();
        customer.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        customer.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await db.SaveChangesAsync(ct);
        return await ToDetailAsync(customer, ct);
    }

    private async Task<CustomerDetailDto> ToDetailAsync(Customer c, CancellationToken ct)
    {
        var history = await agenda.GetCustomerHistoryAsync(c.Id, ct);
        var zone = await ZoneAsync(ct);
        return new CustomerDetailDto(
            c.Id, c.Name, c.Phone, c.Email, c.Notes, BusinessTime.ToLocal(c.CreatedAt, zone), c.PrivacyConsentAt is not null,
            history.Count(a => a.Status == AppointmentStatus.Completed),
            history.Count(a => a.Status == AppointmentStatus.NoShow),
            history);
    }

    private async Task<TimeZoneInfo> ZoneAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        return BusinessTime.Zone(await db.Tenants.Where(t => t.Id == tenantId).Select(t => t.TimeZone).FirstAsync(ct));
    }
}
