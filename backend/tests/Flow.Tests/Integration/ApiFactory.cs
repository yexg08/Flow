using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Flow.Application.Abstractions;
using Flow.Application.Common;
using Flow.Infrastructure.Identity;
using Flow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Flow.Tests.Integration;

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}

/// <summary>
/// Arranca la API completa (Program.cs real) contra un PostgreSQL de verdad en Docker (Testcontainers), con las
/// migraciones reales. Una sola base para todas las pruebas: cada prueba crea sus propios negocios con datos únicos.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SuperAdminEmail = "superadmin@flow.test";
    public const string SuperAdminPassword = "SuperAdmin2026";
    public const string OwnerPassword = "Negocio2026";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("flow_tests")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Server; // Arranca la API ahora: aplica las migraciones y crea el superadmin.
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Key", new string('k', 64));
        builder.UseSetting("Links:Key", new string('l', 64));
        builder.UseSetting("Seed:SuperAdminEmail", SuperAdminEmail);
        builder.UseSetting("Seed:SuperAdminPassword", SuperAdminPassword);
        builder.UseSetting("RateLimiting:AuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:SlugCheckPerMinute", "100000");
        builder.UseSetting("RateLimiting:PublicPerMinute", "100000");
        builder.UseSetting("RateLimiting:BookingPerMinute", "100000");
    }

    /// <summary>Cliente por https: la cookie de refresh es Secure y no viaja por http.</summary>
    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    public sealed record Business(HttpClient Client, Guid TenantId, string Slug, string Email);

    /// <summary>Registra un negocio nuevo (datos únicos) y devuelve su cliente HTTP con la sesión del dueño.</summary>
    public async Task<Business> RegisterBusinessAsync(string name = "Negocio de prueba")
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        var slug = $"negocio-{unique}";
        var email = $"dueno.{unique}@flow.test";
        var client = NewClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { businessName = name, slug, fullName = "Dueña de prueba", email, password = OwnerPassword });
        await Expect(response, HttpStatusCode.Created);

        var body = await Json(response);
        Authorize(client, body);
        var tenantId = body.GetProperty("user").GetProperty("tenant").GetProperty("id").GetGuid();
        return new Business(client, tenantId, slug, email);
    }

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        await Expect(response, HttpStatusCode.OK);
        Authorize(client, await Json(response));
        return client;
    }

    public Task<HttpClient> LoginAsSuperAdminAsync() => LoginAsync(SuperAdminEmail, SuperAdminPassword);

    /// <summary>Crea una cuenta de empleado en el negocio y devuelve su cliente HTTP con sesión.</summary>
    public async Task<HttpClient> CreateStaffAccountAsync(Guid tenantId)
    {
        var email = $"empleado.{Guid.NewGuid():N}@flow.test";
        using (var scope = Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, FullName = "Empleado", TenantId = tenantId, CreatedAt = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(user, OwnerPassword)).Succeeded);
            await users.AddToRoleAsync(user, Roles.Staff);
        }
        return await LoginAsync(email, OwnerPassword);
    }

    /// <summary>Ejecuta código contra la base con un negocio fijado a mano (como lo haría un proceso sin petición).</summary>
    public async Task<T> WithDbAsync<T>(Guid? tenantId, Func<AppDbContext, IServiceProvider, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(tenantId);
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>(), scope.ServiceProvider);
    }

    public static void Authorize(HttpClient client, JsonElement authBody) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authBody.GetProperty("accessToken").GetString());

    public static async Task Expect(HttpResponseMessage response, HttpStatusCode status)
    {
        if (response.StatusCode == status) return;
        var body = await response.Content.ReadAsStringAsync();
        throw new Xunit.Sdk.XunitException(
            $"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: se esperaba {(int)status} " +
            $"y llegó {(int)response.StatusCode}. Cuerpo: {body}");
    }

    public static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static object NewService(string name = "Corte de cabello", int minutes = 30, decimal price = 25000, bool isActive = true) =>
        new { name, description = (string?)null, durationMinutes = minutes, price, isActive };

    public static async Task<Guid> CreateServiceAsync(HttpClient owner, string name = "Corte de cabello", bool isActive = true)
    {
        var response = await owner.PostAsJsonAsync("/api/services", NewService(name, isActive: isActive));
        await Expect(response, HttpStatusCode.Created);
        return (await Json(response)).GetProperty("id").GetGuid();
    }
}
