using Flow.Application.Abstractions;
using Flow.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Public;

public record PublicServiceDto(Guid Id, string Name, string? Description, int DurationMinutes, decimal Price);

public record PublicBusinessDto(
    string Name, string Slug, string AccentColor, string Currency, string? WhatsApp, IReadOnlyList<PublicServiceDto> Services);

public interface IPublicBusinessService
{
    Task<PublicBusinessDto> GetBySlugAsync(string slug, CancellationToken ct);
}

/// <summary>
/// Página pública de un negocio. Es el ÚNICO lugar que lee datos de negocio sin sesión: por eso ignora el filtro
/// global y filtra a mano por el negocio del slug. Solo expone campos pensados para el público (DTOs propios).
/// </summary>
public class PublicBusinessService(IAppDbContext db) : IPublicBusinessService
{
    public async Task<PublicBusinessDto> GetBySlugAsync(string slug, CancellationToken ct)
    {
        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Slug == slug && t.IsActive)
            .Select(t => new { t.Id, t.Name, t.Slug, t.AccentColor, t.Currency, t.WhatsApp })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("El negocio");

        var services = await db.Services.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new PublicServiceDto(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price))
            .ToListAsync(ct);

        return new PublicBusinessDto(tenant.Name, tenant.Slug, tenant.AccentColor, tenant.Currency, tenant.WhatsApp, services);
    }
}
