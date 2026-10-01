using Flow.Application.Abstractions;
using Flow.Application.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Flow.Application.Business;

public record BusinessDto(Guid Id, string Name, string Slug, string TimeZone, string Currency, string? WhatsApp, string AccentColor);

public record UpdateBusinessRequest(string Name, string Slug, string TimeZone, string? WhatsApp, string AccentColor);

public class UpdateBusinessRequestValidator : AbstractValidator<UpdateBusinessRequest>
{
    public UpdateBusinessRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(80).WithMessage("El nombre no puede superar 80 caracteres.");
        RuleFor(x => x.Slug).ValidSlug();
        RuleFor(x => x.TimeZone)
            .Must(tz => tz is not null && TimeZoneInfo.TryFindSystemTimeZoneById(tz, out _))
            .WithMessage("La zona horaria no es válida.");
        RuleFor(x => x.WhatsApp).OptionalWhatsApp();
        RuleFor(x => x.AccentColor).HexColor();
    }
}

public interface IBusinessService
{
    Task<BusinessDto> GetCurrentAsync(CancellationToken ct);
    Task<BusinessDto> UpdateCurrentAsync(UpdateBusinessRequest request, CancellationToken ct);
}

/// <summary>Ajustes del negocio de la sesión. Siempre opera sobre el TenantId de la sesión, nunca uno recibido.</summary>
public class BusinessService(IAppDbContext db, ITenantContext tenant) : IBusinessService
{
    public async Task<BusinessDto> GetCurrentAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new NotFoundException("El negocio");
        return new BusinessDto(t.Id, t.Name, t.Slug, t.TimeZone, t.Currency, t.WhatsApp, t.AccentColor);
    }

    public async Task<BusinessDto> UpdateCurrentAsync(UpdateBusinessRequest request, CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var t = await db.Tenants.FirstOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new NotFoundException("El negocio");

        var slug = request.Slug.Trim();
        if (slug != t.Slug && await db.Tenants.AnyAsync(x => x.Slug == slug && x.Id != tenantId, ct))
            throw new FieldValidationException(nameof(request.Slug), "Ese enlace ya lo usa otro negocio.");

        t.Name = request.Name.Trim();
        t.Slug = slug;
        t.TimeZone = request.TimeZone;
        t.WhatsApp = string.IsNullOrWhiteSpace(request.WhatsApp) ? null : request.WhatsApp.Trim();
        t.AccentColor = request.AccentColor.ToLowerInvariant();
        await db.SaveChangesAsync(ct);

        return new BusinessDto(t.Id, t.Name, t.Slug, t.TimeZone, t.Currency, t.WhatsApp, t.AccentColor);
    }
}
