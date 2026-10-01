using Flow.Application.Abstractions;
using Flow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Public;

public record PublicServiceDto(Guid Id, string Name, string? Description, int DurationMinutes, decimal Price);

public record PublicStaffDto(Guid Id, string Name, string Color, IReadOnlyList<Guid> ServiceIds);

public record PublicBusinessDto(
    string Name, string Slug, string AccentColor, string Currency, string? WhatsApp,
    IReadOnlyList<PublicServiceDto> Services, IReadOnlyList<PublicStaffDto> Staff);

public interface IPublicBusinessService
{
    Task<PublicBusinessDto> GetBySlugAsync(string slug, CancellationToken ct);
}

/// <summary>
/// Página pública de un negocio. Sin sesión: el negocio sale del enlace y se fija como negocio de la petición, así el
/// filtro global limita todo a ese negocio. Solo expone campos pensados para el público (DTOs propios).
/// </summary>
public class PublicBusinessService(IAppDbContext db, ITenantContext tenantContext) : IPublicBusinessService
{
    public async Task<PublicBusinessDto> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Slug == slug && t.IsActive)
            .Select(t => new { t.Id, t.Name, t.Slug, t.AccentColor, t.Currency, t.WhatsApp })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("El negocio");

        tenantContext.Set(tenant.Id);

        var services = await db.Services.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new PublicServiceDto(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price))
            .ToListAsync(ct);

        // Solo quien puede recibir reservas: activo y con horario.
        var staff = await db.Staff.AsNoTracking()
            .Where(s => s.IsActive && s.WorkingHours.Any())
            .OrderBy(s => s.Name)
            .Select(s => new PublicStaffDto(s.Id, s.Name, s.Color, s.Services.Select(x => x.ServiceId).ToList()))
            .ToListAsync(ct);

        return new PublicBusinessDto(tenant.Name, tenant.Slug, tenant.AccentColor, tenant.Currency, tenant.WhatsApp, services, staff);
    }
}
