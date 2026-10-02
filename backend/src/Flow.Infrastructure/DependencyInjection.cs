using Flow.Application.Abstractions;
using Flow.Application.Admin;
using Flow.Application.Auth;
using Flow.Application.Team;
using Flow.Infrastructure.Team;
using Flow.Infrastructure.Admin;
using Flow.Infrastructure.Auth;
using Flow.Infrastructure.Identity;
using Flow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Default.");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<TimestampInterceptor>();
        services.AddScoped<TenantInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>(), sp.GetRequiredService<TenantInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddErrorDescriber<SpanishIdentityErrorDescriber>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.Key.Length >= 32, "Jwt:Key debe tener al menos 32 caracteres (configúralo con user-secrets).")
            .ValidateOnStart();

        services.AddOptions<LinkOptions>()
            .Bind(configuration.GetSection(LinkOptions.SectionName))
            .Validate(o => o.Key.Length >= 32, "Links:Key debe tener al menos 32 caracteres (configúralo con user-secrets).")
            .ValidateOnStart();
        services.AddSingleton<IManageLinks, ManageLinks>();

        services.AddScoped<TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ITeamAccountsService, TeamAccountsService>();

        return services;
    }
}
