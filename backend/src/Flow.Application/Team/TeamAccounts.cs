using FluentValidation;

namespace Flow.Application.Team;

public record GrantAccessRequest(string Email);

/// <summary>La contraseña temporal va UNA sola vez en esta respuesta: en la base solo queda su hash.</summary>
public record TemporaryPasswordDto(string Email, string TemporaryPassword, DateTime ExpiresAt);

/// <param name="PendingFirstLogin">Aún no ha cambiado la contraseña temporal.</param>
public record StaffAccountDto(Guid StaffMemberId, string Email, DateTime? LastLoginAt, bool PendingFirstLogin, DateTime? TemporaryPasswordExpiresAt);

public class GrantAccessRequestValidator : AbstractValidator<GrantAccessRequest>
{
    public GrantAccessRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Escribe el correo de la persona.")
            .EmailAddress().WithMessage("El correo no es válido.")
            .MaximumLength(256).WithMessage("El correo es demasiado largo.");
    }
}

/// <summary>
/// Cuentas con las que el equipo entra al panel (rol empleado). Solo el dueño las gestiona. El acceso se da con una
/// contraseña temporal que vence a los 7 días y obliga a cambiarla en el primer ingreso.
/// </summary>
public interface ITeamAccountsService
{
    Task<IReadOnlyList<StaffAccountDto>> ListAsync(CancellationToken ct);
    Task<TemporaryPasswordDto> GrantAsync(Guid staffMemberId, GrantAccessRequest request, CancellationToken ct);
    /// <summary>Contraseña temporal nueva: la anterior deja de servir y se cierran sus sesiones.</summary>
    Task<TemporaryPasswordDto> ResetPasswordAsync(Guid staffMemberId, CancellationToken ct);
    /// <summary>Borra la cuenta: sus sesiones abiertas se cortan de inmediato.</summary>
    Task RevokeAsync(Guid staffMemberId, CancellationToken ct);
    /// <summary>Borra a la persona del equipo y, si tenía, su cuenta. Todo o nada.</summary>
    Task DeleteStaffAsync(Guid staffMemberId, CancellationToken ct);
}
