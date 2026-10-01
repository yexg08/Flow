using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Catalog;

public record ServiceDto(Guid Id, string Name, string? Description, int DurationMinutes, decimal Price, bool IsActive);

public record ServiceUpsertRequest(string Name, string? Description, int DurationMinutes, decimal Price, bool IsActive = true);

public class ServiceUpsertRequestValidator : AbstractValidator<ServiceUpsertRequest>
{
    public ServiceUpsertRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres.");
        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("La descripción no puede superar 500 caracteres.");
        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(5, 480).WithMessage("La duración debe estar entre 5 minutos y 8 horas.")
            .Must(m => m % 5 == 0).WithMessage("La duración debe ser múltiplo de 5 minutos.");
        RuleFor(x => x.Price)
            .InclusiveBetween(0, 100_000_000).WithMessage("El precio debe estar entre 0 y 100.000.000.");
    }
}

public interface ICatalogService
{
    Task<IReadOnlyList<ServiceDto>> ListAsync(CancellationToken ct);
    Task<ServiceDto> GetAsync(Guid id, CancellationToken ct);
    Task<ServiceDto> CreateAsync(ServiceUpsertRequest request, CancellationToken ct);
    Task<ServiceDto> UpdateAsync(Guid id, ServiceUpsertRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// Servicios del negocio de la sesión. No filtra por TenantId a mano: lo hace el filtro global de EF Core, así que
/// pedir el id de un servicio de otro negocio da 404, igual que un id inexistente.
/// </summary>
public class CatalogService(IAppDbContext db) : ICatalogService
{
    private const string Resource = "El servicio";

    public async Task<IReadOnlyList<ServiceDto>> ListAsync(CancellationToken ct) =>
        await db.Services.AsNoTracking()
            .OrderByDescending(s => s.IsActive).ThenBy(s => s.Name)
            .Select(s => ToDto(s))
            .ToListAsync(ct);

    public async Task<ServiceDto> GetAsync(Guid id, CancellationToken ct) => ToDto(await FindAsync(id, ct));

    public async Task<ServiceDto> CreateAsync(ServiceUpsertRequest request, CancellationToken ct)
    {
        var service = new BookableService();
        Apply(service, request);
        db.Services.Add(service);
        await db.SaveChangesAsync(ct);
        return ToDto(service);
    }

    public async Task<ServiceDto> UpdateAsync(Guid id, ServiceUpsertRequest request, CancellationToken ct)
    {
        var service = await FindAsync(id, ct);
        Apply(service, request);
        await db.SaveChangesAsync(ct);
        return ToDto(service);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var service = await FindAsync(id, ct);
        db.Services.Remove(service);
        await db.SaveChangesAsync(ct);
    }

    private async Task<BookableService> FindAsync(Guid id, CancellationToken ct) =>
        await db.Services.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(Resource);

    private static void Apply(BookableService service, ServiceUpsertRequest request)
    {
        service.Name = request.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        service.DurationMinutes = request.DurationMinutes;
        service.Price = request.Price;
        service.IsActive = request.IsActive;
    }

    private static ServiceDto ToDto(BookableService s) =>
        new(s.Id, s.Name, s.Description, s.DurationMinutes, s.Price, s.IsActive);
}
