using Flow.Application.Common;
using Flow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Persistence;

/// <summary>
/// Al arrancar: aplica las migraciones, crea los roles y la cuenta de superadmin.
/// La contraseña del superadmin se lee de configuración (user-secrets): Seed:SuperAdminPassword.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));
        var db = provider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        var roleManager = provider.GetRequiredService<RoleManager<AppRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new AppRole(role));
        }

        var config = provider.GetRequiredService<IConfiguration>();
        var email = config["Seed:SuperAdminEmail"];
        var password = config["Seed:SuperAdminPassword"];
        if (string.IsNullOrWhiteSpace(email)) return;

        var userManager = provider.GetRequiredService<UserManager<AppUser>>();
        if (await userManager.FindByEmailAsync(email) is not null) return;

        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No se creó el superadmin {Email}: falta Seed:SuperAdminPassword (ver README).", email);
            return;
        }

        var admin = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = config["Seed:SuperAdminFullName"] ?? "Superadmin",
            CreatedAt = DateTime.UtcNow
        };
        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("No se pudo crear el superadmin: {Errors}", string.Join(" ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
        logger.LogInformation("Superadmin {Email} creado.", email);
    }
}
