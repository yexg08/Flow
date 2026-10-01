namespace Flow.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Lo llena el interceptor de timestamps (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Dato que pertenece a un negocio. El filtro global de EF Core solo deja ver los del negocio de la sesión, y el
/// interceptor de tenant llena el TenantId al crear y bloquea escrituras sobre datos de otro negocio.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; set; }
}
