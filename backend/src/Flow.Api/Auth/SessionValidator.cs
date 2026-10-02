using System.Security.Claims;
using Flow.Infrastructure.Auth;
using Flow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace Flow.Api.Auth;

/// <summary>
/// Se ejecuta en CADA petición autenticada, después de validar la firma del JWT. Consulta la base de datos para
/// que los cambios sean inmediatos (sin esperar los 15 minutos que dura el token):
/// - Cuenta desactivada o negocio suspendido: rechazada.
/// - Sello de seguridad distinto (contraseña cambiada): rechazada.
/// - Agrega el TenantId leído de la base de datos. Ese claim es la única fuente del negocio de la sesión.
/// </summary>
public static class SessionValidator
{
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity is not ClaimsIdentity identity
            || !Guid.TryParse(principal.FindFirstValue(AppClaims.UserId), out var userId))
        {
            context.Fail("Token sin usuario.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var session = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.IsActive,
                u.SecurityStamp,
                u.TenantId,
                u.MustChangePassword,
                TenantActive = u.TenantId == null || db.Tenants.Any(t => t.Id == u.TenantId && t.IsActive)
            })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

        if (session is null || !session.IsActive || !session.TenantActive)
        {
            context.Fail("Cuenta inactiva o negocio suspendido.");
            return;
        }

        if (principal.FindFirstValue(AppClaims.SecurityStamp) != session.SecurityStamp)
        {
            context.Fail("Sesión revocada.");
            return;
        }

        // Por si alguien fabricara un token con estos claims: se descartan y se reconstruyen desde la base.
        foreach (var claim in identity.FindAll(c => c.Type is AppClaims.TenantId or AppClaims.PasswordChangeRequired).ToList())
            identity.RemoveClaim(claim);
        if (session.TenantId is { } tenantId) identity.AddClaim(new Claim(AppClaims.TenantId, tenantId.ToString()));
        if (session.MustChangePassword) identity.AddClaim(new Claim(AppClaims.PasswordChangeRequired, "true"));
    }
}
