using Flow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flow.Infrastructure.Persistence;

/// <summary>Solo para `dotnet ef migrations`: no necesita la API ni una sesión.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5434;Database=flow;Username=flow;Password=flow_dev_password")
            .Options;
        return new AppDbContext(options, new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public void Set(Guid? tenantId) { }
    }
}
