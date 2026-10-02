using Microsoft.AspNetCore.Identity;

namespace Flow.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public AppUser() => Id = Guid.CreateVersion7();

    public string FullName { get; set; } = string.Empty;

    /// <summary>Negocio al que pertenece la cuenta. Null solo para el superadmin.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Desactivar corta el acceso de inmediato (se revisa en cada petición).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// La contraseña actual es temporal (la generó el dueño del negocio): hasta cambiarla, la cuenta solo puede usar
    /// el endpoint de cambio de contraseña.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Hasta cuándo sirve la contraseña temporal (7 días desde que se generó).</summary>
    public DateTime? TemporaryPasswordExpiresAt { get; set; }

    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppRole : IdentityRole<Guid>
{
    public AppRole() => Id = Guid.CreateVersion7();

    public AppRole(string name) : this() => Name = name;
}

/// <summary>Mensajes de Identity en español (los que pueden llegar a verse en pantalla).</summary>
public class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "La contraseña actual no es correcta." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"La contraseña debe tener al menos {length} caracteres." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "La contraseña debe tener al menos un número." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "La contraseña debe tener al menos una letra minúscula." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Ya existe una cuenta con ese correo." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Ya existe una cuenta con ese correo." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "El correo no es válido." };
}
