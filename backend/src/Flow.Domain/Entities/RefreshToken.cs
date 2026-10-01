namespace Flow.Domain.Entities;

/// <summary>Refresh token guardado como hash SHA-256. Se rota en cada uso.</summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}
