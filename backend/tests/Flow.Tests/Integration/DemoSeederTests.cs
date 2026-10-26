using System.Net;
using Flow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flow.Tests.Integration;

/// <summary>
/// los datos de ejemplo (opcionales y solo para probar en local) no deben romper nada
/// se cargan una sola vez y cada negocio ve solo lo suyo y las cuentas tienen los permisos de su rol
/// </summary>
[Collection(ApiCollection.Name)]
public class DemoSeederTests(ApiFactory factory)
{
    private const string DemoPassword = "Demo2026Flow";

    private sealed record Counts(int Services, int Staff, int Customers, int Appointments);

    private async Task<Counts> CountsAsync(string slug)
    {
        var tenantId = await factory.WithDbAsync(null, (db, _) =>
            db.Tenants.Where(t => t.Slug == slug).Select(t => t.Id).SingleAsync());

        return await factory.WithDbAsync(tenantId, async (db, _) => new Counts(
            await db.Services.CountAsync(), await db.Staff.CountAsync(),
            await db.Customers.CountAsync(), await db.Appointments.CountAsync()));
    }

    [Fact]
    public async Task Demo_data_is_loaded_once_and_each_business_sees_only_its_own()
    {
        await DemoSeeder.SeedAsync(factory.Services, DemoPassword);
        var barbershop = await CountsAsync("barberia-don-mateo");
        var spa = await CountsAsync("spa-montana");

        Assert.Equal(5, barbershop.Services);
        Assert.Equal(3, barbershop.Staff);
        Assert.True(barbershop.Appointments > 100, "la agenda de ejemplo debe tener historial");
        Assert.True(spa.Appointments > 100);

        // cargarlos otra vez no duplica nada
        await DemoSeeder.SeedAsync(factory.Services, DemoPassword);
        Assert.Equal(barbershop, await CountsAsync("barberia-don-mateo"));
        Assert.Equal(spa, await CountsAsync("spa-montana"));

        // el dueño de la barberia ve sus servicios y nada del spa
        var owner = await factory.LoginAsync(DemoSeeder.BarbershopEmail, DemoPassword);
        var services = await ApiFactory.Json(await owner.GetAsync("/api/services"));
        Assert.Equal(5, services.GetArrayLength());
        Assert.DoesNotContain(services.EnumerateArray(), s => s.GetProperty("name").GetString() == "Masaje relajante");

        var upcoming = await ApiFactory.Json(await owner.GetAsync("/api/appointments/upcoming?limit=10"));
        Assert.True(upcoming.GetArrayLength() > 0, "debe haber citas proximas");
    }

    [Fact]
    public async Task Demo_staff_account_works_but_cannot_manage_the_business()
    {
        await DemoSeeder.SeedAsync(factory.Services, DemoPassword);

        var staff = await factory.LoginAsync(DemoSeeder.SpaStaffEmail, DemoPassword);
        await ApiFactory.Expect(await staff.GetAsync("/api/business"), HttpStatusCode.OK);
        await ApiFactory.Expect(await staff.GetAsync("/api/business/metrics"), HttpStatusCode.Forbidden);

        var owner = await factory.LoginAsync(DemoSeeder.SpaEmail, DemoPassword);
        await ApiFactory.Expect(await owner.GetAsync("/api/business/metrics"), HttpStatusCode.OK);
    }
}
