using Flow.Application.Abstractions;
using Flow.Application.Common;
using Npgsql;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Flow.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<BookableService> Services => Set<BookableService>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();
    public DbSet<StaffMemberService> StaffServices => Set<StaffMemberService>();
    public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();
    public DbSet<TimeOff> TimeOff => Set<TimeOff>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Lo lee el filtro global en cada consulta (EF Core lo toma de ESTA instancia del contexto, no lo fija al
    /// construir el modelo). Null = sin negocio: el filtro no deja pasar ninguna fila.
    /// </summary>
    private Guid? CurrentTenantId => tenant.TenantId;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Necesaria para la restricción que impide citas cruzadas (combina "=" sobre la persona con "&&" sobre el rango).
        builder.HasPostgresExtension("btree_gist");
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Filtro de negocio en TODAS las entidades ITenantOwned, incluidas las que se agreguen en el futuro.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType)) continue;
            typeof(AppDbContext)
                .GetMethod(nameof(ApplyTenantFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(this, [builder]);
        }
    }

    private void ApplyTenantFilter<T>(ModelBuilder builder) where T : class, ITenantOwned =>
        builder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch (DbUpdateException e) when (IsScheduleConflict(e))
        {
            throw new ScheduleConflictException();
        }
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        }
        catch (DbUpdateException e) when (IsScheduleConflict(e))
        {
            throw new ScheduleConflictException();
        }
    }

    /// <summary>La base rechazó una cita que se cruza con otra (dos reservas al mismo tiempo para el mismo horario).</summary>
    private static bool IsScheduleConflict(DbUpdateException e) =>
        e.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation,
            ConstraintName: AppointmentConfiguration.NoOverlapConstraint
        };
}
