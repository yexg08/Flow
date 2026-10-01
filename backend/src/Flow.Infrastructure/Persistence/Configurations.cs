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
