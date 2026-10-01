using Flow.Application.Auth;
using Flow.Application.Common;
using Flow.Domain.Entities;
using Flow.Infrastructure.Identity;
using Flow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Flow.Infrastructure.Auth;

public class AuthService(
    UserManager<AppUser> userManager,
    AppDbContext db,
    TokenService tokenService,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Correo o contraseña incorrectos.";
    private const string InvalidSession = "La sesión expiró. Inicia sesión de nuevo.";
    public const string AccountDisabled = "Tu cuenta está desactivada.";
    public const string BusinessSuspended = "Este negocio está suspendido. Escríbenos si crees que es un error.";
    private const string SlugTaken = "Ese enlace ya lo usa otro negocio.";

    private static string? _dummyHash;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.Trim();
        var email = request.Email.Trim();

        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
            throw new FieldValidationException(nameof(request.Slug), SlugTaken);
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new FieldValidationException(nameof(request.Email), "Ya existe una cuenta con ese correo.");

        // Negocio, cuenta y rol se crean juntos o no se crea nada.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var tenant = new Tenant { Name = request.BusinessName.Trim(), Slug = slug };
        db.Tenants.Add(tenant);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Otro registro tomó el mismo enlace entre la comprobación y el guardado.
            throw new FieldValidationException(nameof(request.Slug), SlugTaken);
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim(),
            TenantId = tenant.Id,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            LastLoginAt = timeProvider.GetUtcNow().UtcDateTime
        };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            var error = created.Errors.First();
            var field = error.Code.Contains("Password") ? nameof(request.Password) : nameof(request.Email);
            throw new FieldValidationException(field, error.Description);
        }

        await userManager.AddToRoleAsync(user, Roles.Owner);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Negocio registrado: {Slug} ({TenantId})", tenant.Slug, tenant.Id);
        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Misma demora que con un correo existente: así no se puede averiguar qué correos tienen cuenta.
            var dummy = new AppUser();
            _dummyHash ??= userManager.PasswordHasher.HashPassword(dummy, Guid.NewGuid().ToString());
            userManager.PasswordHasher.VerifyHashedPassword(dummy, _dummyHash, request.Password);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            logger.LogWarning("Intento de inicio de sesión en cuenta bloqueada {UserId}", user.Id);
            throw new AuthenticationFailedException(
                "Cuenta bloqueada temporalmente por intentos fallidos. Intenta de nuevo en unos minutos.");
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Contraseña incorrecta para {UserId}", user.Id);
            if (await userManager.IsLockedOutAsync(user))
                logger.LogWarning("Cuenta {UserId} bloqueada por intentos fallidos", user.Id);
            throw new AuthenticationFailedException(InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        // Estos motivos se revelan solo DESPUÉS de verificar la contraseña.
        await EnsureCanSignInAsync(user, ct);

        user.LastLoginAt = timeProvider.GetUtcNow().UtcDateTime;
        await userManager.UpdateAsync(user);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var stored = await FindTokenAsync(refreshToken, ct) ?? throw new AuthenticationFailedException(InvalidSession);

        if (stored.RevokedAt is not null)
        {
            // Un token ya rotado se volvió a usar: posible robo. Se cierran todas las sesiones del usuario.
            logger.LogWarning("Reuso de refresh token revocado del usuario {UserId}: se cierran sus sesiones", stored.UserId);
            await RevokeAllAsync(db, stored.UserId, now, ct);
            throw new AuthenticationFailedException(InvalidSession);
        }

        if (!stored.IsActive(now)) throw new AuthenticationFailedException(InvalidSession);

        var user = await userManager.FindByIdAsync(stored.UserId.ToString())
            ?? throw new AuthenticationFailedException(InvalidSession);

        try
        {
            await EnsureCanSignInAsync(user, ct);
        }
        catch (AuthenticationFailedException)
        {
            await RevokeAllAsync(db, user.Id, now, ct);
            throw;
        }

        return await IssueTokensAsync(user, ct, replacing: stored);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var stored = await FindTokenAsync(refreshToken, ct);
        if (stored is null || stored.RevokedAt is not null) return;

        stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDto> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("El usuario");
        return await ToDtoAsync(user, ct);
    }

    public async Task<SlugAvailabilityDto> CheckSlugAsync(string slug, CancellationToken ct = default)
    {
        slug = slug.Trim();
        if (!Slugs.HasValidFormat(slug))
            return new SlugAvailabilityDto(slug, false, "Usa letras minúsculas, números o guiones (de 3 a 40).");
        if (Slugs.IsReserved(slug)) return new SlugAvailabilityDto(slug, false, "Ese enlace está reservado.");
        if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct)) return new SlugAvailabilityDto(slug, false, SlugTaken);
        return new SlugAvailabilityDto(slug, true, null);
    }

    /// <summary>Cuenta activa y, si pertenece a un negocio, que el negocio no esté suspendido.</summary>
    private async Task EnsureCanSignInAsync(AppUser user, CancellationToken ct)
    {
        if (!user.IsActive) throw new AuthenticationFailedException(AccountDisabled);
        if (user.TenantId is { } tenantId && !await db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive, ct))
            throw new AuthenticationFailedException(BusinessSuspended);
    }

    private async Task<AuthResult> IssueTokensAsync(AppUser user, CancellationToken ct, RefreshToken? replacing = null)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dto = await ToDtoAsync(user, ct);
        var (accessToken, accessExpiresAt) = tokenService.CreateAccessToken(
            user.Id, dto.Email, dto.FullName, [dto.Role], await userManager.GetSecurityStampAsync(user));

        var rawRefresh = TokenService.GenerateRefreshToken();
        var refreshHash = TokenService.HashRefreshToken(rawRefresh);
        var refreshExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays);

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedByTokenHash = refreshHash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = now,
            ExpiresAt = refreshExpiresAt
        });
        await db.SaveChangesAsync(ct);

        return new AuthResult(new AuthResponse(accessToken, accessExpiresAt, dto), rawRefresh, refreshExpiresAt);
    }

    private Task<RefreshToken?> FindTokenAsync(string rawToken, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(rawToken);
        return db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
    }

    public static async Task RevokeAllAsync(AppDbContext db, Guid userId, DateTime now, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var token in active) token.RevokedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private async Task<UserDto> ToDtoAsync(AppUser user, CancellationToken ct)
    {
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? Roles.Staff;
        TenantSummaryDto? tenant = null;
        if (user.TenantId is { } tenantId)
        {
            tenant = await db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => new TenantSummaryDto(t.Id, t.Name, t.Slug))
                .FirstOrDefaultAsync(ct);
        }

        return new UserDto(user.Id, user.Email ?? string.Empty, user.FullName, role, tenant);
    }
}
