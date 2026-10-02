using Flow.Application.Common;
using FluentValidation;

namespace Flow.Application.Auth;

public record RegisterRequest(string BusinessName, string Slug, string FullName, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record TenantSummaryDto(Guid Id, string Name, string Slug);

/// <param name="StaffMemberId">Si la cuenta es de un empleado: la persona del equipo a la que corresponde.</param>
public record UserDto(
    Guid Id, string Email, string FullName, string Role, TenantSummaryDto? Tenant, bool MustChangePassword, Guid? StaffMemberId);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Escribe tu contraseña actual.");
        RuleFor(x => x.NewPassword)
            .ValidPassword()
            .NotEqual(x => x.CurrentPassword).WithMessage("La nueva contraseña debe ser distinta a la actual.");
    }
}

public record AuthResponse(string AccessToken, DateTime AccessTokenExpiresAt, UserDto User);

/// <summary>Lo que devuelve el servicio: la respuesta JSON y el refresh token (que va en una cookie httpOnly).</summary>
public record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);

public record SlugAvailabilityDto(string Slug, bool Available, string? Reason);

public interface IAuthService
{
    /// <summary>Registro abierto: crea el negocio y la cuenta de su dueño, y deja la sesión iniciada.</summary>
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
    Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task<SlugAvailabilityDto> CheckSlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Cambia la contraseña, cierra las demás sesiones y devuelve tokens nuevos para esta.</summary>
    Task<AuthResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.BusinessName)
            .NotEmpty().WithMessage("El nombre del negocio es obligatorio.")
            .MinimumLength(2).WithMessage("El nombre del negocio es muy corto.")
            .MaximumLength(80).WithMessage("El nombre del negocio no puede superar 80 caracteres.");
        RuleFor(x => x.Slug).ValidSlug();
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Tu nombre es obligatorio.")
            .MaximumLength(80).WithMessage("El nombre no puede superar 80 caracteres.");
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no es válido.")
            .MaximumLength(256).WithMessage("El correo es demasiado largo.");
        RuleFor(x => x.Password).ValidPassword();
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo es demasiado largo.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(128).WithMessage("La contraseña es demasiado larga.");
    }
}
