using Flow.Domain.Entities;
using Flow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flow.Infrastructure.Persistence;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.Property(t => t.Name).HasMaxLength(80).IsRequired();
        b.Property(t => t.Slug).HasMaxLength(40).IsRequired();
        b.HasIndex(t => t.Slug).IsUnique();
        b.Property(t => t.TimeZone).HasMaxLength(64).IsRequired();
        b.Property(t => t.Currency).HasMaxLength(3).IsRequired();
        b.Property(t => t.WhatsApp).HasMaxLength(15);
        b.Property(t => t.AccentColor).HasMaxLength(7).IsRequired();
    }
}

public class BookableServiceConfiguration : IEntityTypeConfiguration<BookableService>
{
    public void Configure(EntityTypeBuilder<BookableService> b)
    {
        b.ToTable("services");
        b.Property(s => s.Name).HasMaxLength(100).IsRequired();
        b.Property(s => s.Description).HasMaxLength(500);
        b.Property(s => s.Price).HasPrecision(14, 2);
        b.HasIndex(s => s.TenantId);
        b.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> b)
    {
        b.ToTable("staff_members");
        b.Property(s => s.Name).HasMaxLength(80).IsRequired();
        b.Property(s => s.Color).HasMaxLength(7).IsRequired();
        b.HasIndex(s => s.TenantId);
        b.HasOne<Tenant>().WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
        // Al borrar a la persona se borran sus horarios, sus servicios y sus bloqueos (lo hace la base de datos).
        b.HasMany(s => s.Services).WithOne().HasForeignKey(x => x.StaffMemberId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(s => s.WorkingHours).WithOne().HasForeignKey(x => x.StaffMemberId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StaffMemberServiceConfiguration : IEntityTypeConfiguration<StaffMemberService>
{
    public void Configure(EntityTypeBuilder<StaffMemberService> b)
    {
        b.ToTable("staff_member_services");
        b.HasKey(x => new { x.StaffMemberId, x.ServiceId });
        b.HasIndex(x => x.ServiceId);
        b.HasIndex(x => x.TenantId);
        // Al borrar un servicio, deja de estar asignado a las personas que lo hacían.
        b.HasOne<BookableService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkingHoursConfiguration : IEntityTypeConfiguration<WorkingHours>
{
    public void Configure(EntityTypeBuilder<WorkingHours> b)
    {
        b.ToTable("working_hours");
        b.HasIndex(x => new { x.TenantId, x.StaffMemberId });
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_working_hours_range", "\"End\" > \"Start\""));
    }
}

public class TimeOffConfiguration : IEntityTypeConfiguration<TimeOff>
{
    public void Configure(EntityTypeBuilder<TimeOff> b)
    {
        b.ToTable("time_off", t => t.HasCheckConstraint("ck_time_off_range", "\"EndsAtUtc\" > \"StartsAtUtc\""));
        b.Property(x => x.Reason).HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.EndsAtUtc });
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StaffMember>().WithMany().HasForeignKey(x => x.StaffMemberId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customers");
        b.Property(c => c.Name).HasMaxLength(80).IsRequired();
        b.Property(c => c.Phone).HasMaxLength(15).IsRequired();
        b.Property(c => c.Email).HasMaxLength(256);
        b.HasIndex(c => new { c.TenantId, c.Phone }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(c => c.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// La restricción que impide citas cruzadas (EXCLUDE USING gist) no se puede expresar con EF: se crea con SQL en la
/// migración Booking. Se llama ex_appointments_no_overlap.
/// </summary>
public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public const string NoOverlapConstraint = "ex_appointments_no_overlap";

    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("appointments", t => t.HasCheckConstraint("ck_appointments_range", "\"EndsAtUtc\" > \"StartsAtUtc\""));
        b.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(a => a.Price).HasPrecision(14, 2);
        b.Property(a => a.CustomerNote).HasMaxLength(300);
        b.Property(a => a.ManageTokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(a => a.ManageTokenHash).IsUnique();
        b.HasIndex(a => new { a.TenantId, a.StartsAtUtc });
        b.HasIndex(a => new { a.StaffMemberId, a.StartsAtUtc });
        b.HasIndex(a => a.CustomerId);
        b.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        // Restrict: un servicio o una persona con citas no se borra (se desactiva), para no perder el historial.
        b.HasOne<BookableService>().WithMany().HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<StaffMember>().WithMany().HasForeignKey(a => a.StaffMemberId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(a => a.CustomerId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
        b.HasIndex(t => t.UserId);
        b.HasOne<AppUser>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.Property(u => u.FullName).HasMaxLength(80).IsRequired();
        b.HasIndex(u => u.TenantId);
        b.HasOne<Tenant>().WithMany().HasForeignKey(u => u.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
