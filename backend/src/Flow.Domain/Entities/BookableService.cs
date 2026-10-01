using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>Un servicio que el negocio ofrece y sus clientes pueden reservar (corte de cabello, limpieza dental...).</summary>
public class BookableService : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int DurationMinutes { get; set; }

    /// <summary>En la moneda del negocio.</summary>
    public decimal Price { get; set; }

    /// <summary>Inactivo = no aparece en la página pública, pero se conserva.</summary>
    public bool IsActive { get; set; } = true;
}
