using System.Security.Cryptography;
using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Application.Team;
using Flow.Domain.Entities;
using Flow.Infrastructure.Auth;
using Flow.Infrastructure.Identity;
using Flow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Team;

public class TeamAccountsService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ITenantContext tenant,
    ITeamService team,
    TimeProvider timeProvider,
    ILogger<TeamAccountsService> logger) : ITeamAccountsService
{
    public static readonly TimeSpan TemporaryPasswordLifetime = TimeSpan.FromDays(7);

    public async Task<IReadOnlyList<StaffAccountDto>> ListAsync(CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        return await db.Staff.AsNoTracking()
            .Where(s => s.UserId != null)
            .Join(db.Users.Where(u => u.TenantId == tenantId), s => s.UserId, u => u.Id, (s, u) =>
                new StaffAccountDto(s.Id, u.Email!, u.LastLoginAt, u.MustChangePassword, u.TemporaryPasswordExpiresAt))
            .ToListAsync(ct);
    }

    public async Task<TemporaryPasswordDto> GrantAsync(Guid staffMemberId, GrantAccessRequest request, CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        var staff = await FindStaffAsync(staffMemberId, ct);
        if (staff.UserId is not null) throw new BusinessRuleException($"{staff.Name} ya tiene acceso al panel.");

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new FieldValidationException(nameof(request.Email), "Ya existe una cuenta con ese correo.");

        var password = GeneratePassword();
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.Add(TemporaryPasswordLifetime);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = staff.Name,
            TenantId = tenantId,
            MustChangePassword = true,
            TemporaryPasswordExpiresAt = expiresAt,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded) throw new FieldValidationException(nameof(request.Email), created.Errors.First().Description);
        await userManager.AddToRoleAsync(user, Roles.Staff);

        staff.UserId = user.Id;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Acceso al panel creado para la persona {StaffId} del negocio {TenantId}", staff.Id, tenantId);
        return new TemporaryPasswordDto(email, password, expiresAt);
    }

    public async Task<TemporaryPasswordDto> ResetPasswordAsync(Guid staffMemberId, CancellationToken ct)
    {
        var user = await FindUserAsync(await FindStaffAsync(staffMemberId, ct), ct);
        var password = GeneratePassword();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Reemplaza la contraseña y renueva el sello de seguridad: los access tokens viejos dejan de servir al instante.
        await userManager.RemovePasswordAsync(user);
        var added = await userManager.AddPasswordAsync(user, password);
        if (!added.Succeeded) throw new BusinessRuleException(added.Errors.First().Description);
        await userManager.UpdateSecurityStampAsync(user);

        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = now.Add(TemporaryPasswordLifetime);
        await userManager.UpdateAsync(user);
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await AuthService.RevokeAllAsync(db, user.Id, now, ct);

        logger.LogWarning("Contraseña temporal nueva para la cuenta {UserId}", user.Id);
        return new TemporaryPasswordDto(user.Email!, password, user.TemporaryPasswordExpiresAt.Value);
    }

    public async Task RevokeAsync(Guid staffMemberId, CancellationToken ct)
    {
        var staff = await FindStaffAsync(staffMemberId, ct);
        var user = await FindUserAsync(staff, ct);
        await DeleteUserAsync(staff, user);
    }

    public async Task DeleteStaffAsync(Guid staffMemberId, CancellationToken ct)
    {
        var staff = await FindStaffAsync(staffMemberId, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (staff.UserId is not null) await DeleteUserAsync(staff, await FindUserAsync(staff, ct));
        // Si la persona tiene citas, esto lanza 409 y la transacción se revierte: la cuenta tampoco se borra.
        await team.DeleteAsync(staffMemberId, ct);
        await transaction.CommitAsync(ct);
    }

    private async Task DeleteUserAsync(StaffMember staff, AppUser user)
    {
        staff.UserId = null;
        await db.SaveChangesAsync();
        // Borrar la cuenta borra sus refresh tokens (cascada) y SessionValidator rechaza sus access tokens al instante.
        var deleted = await userManager.DeleteAsync(user);
        if (!deleted.Succeeded) throw new BusinessRuleException(deleted.Errors.First().Description);
        logger.LogWarning("Acceso al panel quitado a la persona {StaffId} (cuenta {UserId})", staff.Id, user.Id);
    }

    /// <summary>Filtro global: una persona de otro negocio no se encuentra (404).</summary>
    private async Task<StaffMember> FindStaffAsync(Guid id, CancellationToken ct) =>
        await db.Staff.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("La persona");

    private async Task<AppUser> FindUserAsync(StaffMember staff, CancellationToken ct)
    {
        var tenantId = tenant.RequireTenantId();
        return (staff.UserId is { } userId
                ? await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
                : null)
            ?? throw new BusinessRuleException($"{staff.Name} no tiene acceso al panel.");
    }

    /// <summary>16 caracteres en 4 grupos, sin caracteres que se confundan (0/O, 1/l/I), con al menos un número.</summary>
    public static string GeneratePassword()
    {
        const string alphabet = "abcdefghjkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";
        while (true)
        {
            var chars = Enumerable.Range(0, 16).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray();
            if (!chars.Any(char.IsDigit) || !chars.Any(char.IsLetter)) continue;
            return string.Join('-', Enumerable.Range(0, 4).Select(i => new string(chars, i * 4, 4)));
        }
    }
}
