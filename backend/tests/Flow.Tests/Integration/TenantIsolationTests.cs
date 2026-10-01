using System.Net;
using System.Net.Http.Json;
using Flow.Domain.Entities;
using Flow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Flow.Tests.Integration;

/// <summary>
/// El corazón del multi-negocio: un negocio nunca puede ver ni tocar los datos de otro, ni siquiera conociendo
/// sus ids. Se prueba por la API y también directamente contra la base (filtro global e interceptor).
/// </summary>
[Collection(ApiCollection.Name)]
public class TenantIsolationTests(ApiFactory factory)
{
    [Fact]
    public async Task A_business_cannot_read_or_change_another_business_services_by_id()
    {
        var a = await factory.RegisterBusinessAsync("Peluquería A");
        var b = await factory.RegisterBusinessAsync("Peluquería B");
        var serviceOfA = await ApiFactory.CreateServiceAsync(a.Client, "Servicio privado de A");

        // B no lo ve en su lista...
        var list = await ApiFactory.Json(await b.Client.GetAsync("/api/services"));
        Assert.Equal(0, list.GetArrayLength());

        // ...y por id recibe 404, igual que con un id inventado (no se revela que existe).
        await ApiFactory.Expect(await b.Client.GetAsync($"/api/services/{serviceOfA}"), HttpStatusCode.NotFound);
        await ApiFactory.Expect(
            await b.Client.PutAsJsonAsync($"/api/services/{serviceOfA}", ApiFactory.NewService("Hackeado")), HttpStatusCode.NotFound);
        await ApiFactory.Expect(await b.Client.DeleteAsync($"/api/services/{serviceOfA}"), HttpStatusCode.NotFound);

        // El servicio de A sigue intacto.
        var stillThere = await ApiFactory.Json(await a.Client.GetAsync($"/api/services/{serviceOfA}"));
        Assert.Equal("Servicio privado de A", stillThere.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Created_services_always_belong_to_the_session_business()
    {
        var a = await factory.RegisterBusinessAsync();
        var b = await factory.RegisterBusinessAsync();

        // Aunque el cuerpo traiga un tenantId ajeno, se ignora: el negocio sale de la sesión.
        var response = await b.Client.PostAsJsonAsync("/api/services", new
        {
            tenantId = a.TenantId, name = "Intento de colarse", description = (string?)null, durationMinutes = 30, price = 1000m, isActive = true
        });
        await ApiFactory.Expect(response, HttpStatusCode.Created);
        var id = (await ApiFactory.Json(response)).GetProperty("id").GetGuid();

        var owner = await factory.WithDbAsync(null, (db, _) =>
            db.Services.IgnoreQueryFilters().Where(s => s.Id == id).Select(s => s.TenantId).SingleAsync());
        Assert.Equal(b.TenantId, owner);
    }

    [Fact]
    public async Task Global_filter_only_returns_rows_of_the_current_business()
    {
        var a = await factory.RegisterBusinessAsync();
        var b = await factory.RegisterBusinessAsync();
        await ApiFactory.CreateServiceAsync(a.Client, "De A");
        await ApiFactory.CreateServiceAsync(b.Client, "De B");

        var seenByA = await factory.WithDbAsync(a.TenantId, (db, _) => db.Services.Select(s => s.TenantId).Distinct().ToListAsync());
        Assert.Equal(new[] { a.TenantId }, seenByA);

        // Sin negocio (superadmin o anónimo) el filtro no deja pasar NADA, aunque haya datos.
        var seenWithoutTenant = await factory.WithDbAsync(null, (db, _) => db.Services.CountAsync());
        Assert.Equal(0, seenWithoutTenant);
    }

    [Fact]
    public async Task Interceptor_blocks_writes_on_another_business_data()
    {
        var a = await factory.RegisterBusinessAsync();
        var b = await factory.RegisterBusinessAsync();
        var serviceOfA = await ApiFactory.CreateServiceAsync(a.Client);

        // Aunque un error de código cargara el dato saltándose el filtro, no se podría guardar.
        await Assert.ThrowsAsync<TenantIsolationException>(() => factory.WithDbAsync(b.TenantId, async (db, _) =>
        {
            var service = await db.Services.IgnoreQueryFilters().SingleAsync(s => s.Id == serviceOfA);
            service.Name = "Modificado por B";
            return await db.SaveChangesAsync();
        }));

        await Assert.ThrowsAsync<TenantIsolationException>(() => factory.WithDbAsync(b.TenantId, async (db, _) =>
        {
            var service = await db.Services.IgnoreQueryFilters().SingleAsync(s => s.Id == serviceOfA);
            db.Services.Remove(service);
            return await db.SaveChangesAsync();
        }));

        await Assert.ThrowsAsync<TenantIsolationException>(() => factory.WithDbAsync(b.TenantId, async (db, _) =>
        {
            db.Services.Add(new BookableService { TenantId = a.TenantId, Name = "Para A", DurationMinutes = 30 });
            return await db.SaveChangesAsync();
        }));

        await Assert.ThrowsAsync<TenantIsolationException>(() => factory.WithDbAsync(null, async (db, _) =>
        {
            db.Services.Add(new BookableService { Name = "Sin negocio", DurationMinutes = 30 });
            return await db.SaveChangesAsync();
        }));

        var name = await factory.WithDbAsync(a.TenantId, (db, _) => db.Services.Where(s => s.Id == serviceOfA).Select(s => s.Name).SingleAsync());
        Assert.Equal("Corte de cabello", name);
    }

    [Fact]
    public async Task Business_settings_are_always_the_session_business()
    {
        var a = await factory.RegisterBusinessAsync("Negocio A");
        var b = await factory.RegisterBusinessAsync("Negocio B");

        var settingsOfB = await ApiFactory.Json(await b.Client.GetAsync("/api/business"));
        Assert.Equal(b.Slug, settingsOfB.GetProperty("slug").GetString());

        // B intenta quedarse con el enlace de A.
        var response = await b.Client.PutAsJsonAsync("/api/business", new
        {
            name = "Negocio B", slug = a.Slug, timeZone = "America/Bogota", whatsApp = (string?)null, accentColor = "#a855f7"
        });
        await ApiFactory.Expect(response, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(response)).GetProperty("errors").TryGetProperty("slug", out _));
    }
}
