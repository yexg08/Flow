using Flow.Application.Common;
using Flow.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Flow.Api.Auth;

/// <summary>
/// Políticas de autorización.
///
/// DENEGAR POR DEFECTO: <see cref="TenantMember"/> es la política de respaldo (FallbackPolicy). Todo endpoint que no
/// declare otra cosa exige una cuenta de un negocio (dueño o empleado) con su negocio cargado desde la base de datos.
/// Así un endpoint nuevo nunca queda abierto al público ni al superadmin por olvido. Excepciones explícitas:
/// - [AllowAnonymous]: registro, login, refresh, logout, disponibilidad de enlace y la página pública.
/// - [Authorize] (solo sesión): /auth/me.
/// - [Authorize(Policy = TenantOwner)]: acciones que solo hace el dueño del negocio.
/// - [Authorize(Policy = SuperAdmin)]: el panel de la plataforma.
/// </summary>
public static class Policies
{
    public const string TenantMember = "TenantMember";
    public const string TenantOwner = "TenantOwner";
    public const string SuperAdmin = "SuperAdmin";

    public static void Configure(AuthorizationOptions options)
    {
        var member = WithTenant().RequireRole(Roles.TenantMembers).Build();

        options.AddPolicy(TenantMember, member);
        options.AddPolicy(TenantOwner, WithTenant().RequireRole(Roles.Owner).Build());
        options.AddPolicy(SuperAdmin, Working().RequireRole(Roles.SuperAdmin).Build());

        options.FallbackPolicy = member;
        options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }

    /// <summary>El claim tenant_id lo agrega SessionValidator desde la base de datos, no viene en el token.</summary>
    private static AuthorizationPolicyBuilder WithTenant() => Working().RequireClaim(AppClaims.TenantId);

    /// <summary>
    /// Sesión que ya puede trabajar: una cuenta con contraseña temporal (claim pwd_change) no pasa ninguna política de
    /// trabajo; solo puede usar /auth/me y /auth/change-password, que piden solo sesión.
    /// </summary>
    private static AuthorizationPolicyBuilder Working() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireAssertion(context => !context.User.HasClaim(c => c.Type == AppClaims.PasswordChangeRequired));
}
