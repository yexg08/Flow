using Flow.Application.Abstractions;
using Flow.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Flow.Infrastructure.Persistence;

/// <summary>Llena CreatedAt/UpdatedAt (UTC) de las entidades.</summary>
public class TimestampInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}

/// <summary>Se intentó escribir un dato de un negocio distinto al de la sesión. Nunca debería pasar.</summary>
public class TenantIsolationException(string message) : InvalidOperationException(message);

/// <summary>
/// Segunda barrera del aislamiento (la primera es el filtro global de lectura):
/// - Al crear un dato de negocio, le pone el TenantId de la sesión. Sin negocio en la sesión, falla.
/// - Rechaza crear, modificar o borrar un dato que pertenezca a otro negocio, o cambiarle el TenantId.
/// </summary>
public class TenantInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;
        var tenantId = tenant.TenantId;

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (tenantId is null)
                        throw new TenantIsolationException("No se puede crear un dato de negocio sin un negocio en la sesión.");
                    if (entry.Entity.TenantId == Guid.Empty)
                        entry.Entity.TenantId = tenantId.Value;
                    else if (entry.Entity.TenantId != tenantId)
                        throw new TenantIsolationException("No se puede crear un dato para otro negocio.");
                    break;

                case EntityState.Modified:
                case EntityState.Deleted:
                    var original = (Guid)entry.Property(nameof(ITenantOwned.TenantId)).OriginalValue!;
                    if (tenantId is null || original != tenantId || entry.Entity.TenantId != original)
                        throw new TenantIsolationException("No se puede modificar un dato de otro negocio.");
                    break;
            }
        }
    }
}
