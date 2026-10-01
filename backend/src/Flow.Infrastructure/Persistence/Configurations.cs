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
