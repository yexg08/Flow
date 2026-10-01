using System.Net;
using System.Net.Http.Json;

namespace Flow.Tests.Integration;

/// <summary>Superadmin (suspender negocios, listado) y página pública.</summary>
[Collection(ApiCollection.Name)]
public class PlatformTests(ApiFactory factory)
{
    [Fact]
    public async Task Suspending_a_business_cuts_its_sessions_login_and_public_page()
    {
        var business = await factory.RegisterBusinessAsync();
        var admin = await factory.LoginAsSuperAdminAsync();
        await ApiFactory.Expect(await business.Client.GetAsync("/api/business"), HttpStatusCode.OK);

        await ApiFactory.Expect(await admin.PatchAsJsonAsync($"/api/admin/tenants/{business.TenantId}/status", new { isActive = false }),
            HttpStatusCode.NoContent);

        // La sesión abierta deja de servir de inmediato, sin esperar a que venza el token.
        await ApiFactory.Expect(await business.Client.GetAsync("/api/business"), HttpStatusCode.Unauthorized);
        await ApiFactory.Expect(await business.Client.PostAsync("/api/auth/refresh", null), HttpStatusCode.Unauthorized);

        var login = await factory.NewClient().PostAsJsonAsync("/api/auth/login", new { email = business.Email, password = ApiFactory.OwnerPassword });
        await ApiFactory.Expect(login, HttpStatusCode.Unauthorized);
        Assert.Contains("suspendido", (await ApiFactory.Json(login)).GetProperty("detail").GetString());

        await ApiFactory.Expect(await factory.NewClient().GetAsync($"/api/public/businesses/{business.Slug}"), HttpStatusCode.NotFound);

        // Al reactivarlo todo vuelve.
        await ApiFactory.Expect(await admin.PatchAsJsonAsync($"/api/admin/tenants/{business.TenantId}/status", new { isActive = true }),
            HttpStatusCode.NoContent);
        await factory.LoginAsync(business.Email, ApiFactory.OwnerPassword);
        await ApiFactory.Expect(await factory.NewClient().GetAsync($"/api/public/businesses/{business.Slug}"), HttpStatusCode.OK);
    }

    [Fact]
    public async Task Superadmin_lists_businesses_with_owner_and_search()
    {
        var business = await factory.RegisterBusinessAsync("Spa Zafiro Único");
        await ApiFactory.CreateServiceAsync(business.Client);
        var admin = await factory.LoginAsSuperAdminAsync();

        var list = await ApiFactory.Json(await admin.GetAsync($"/api/admin/tenants?search={business.Slug}"));
        var row = Assert.Single(list.EnumerateArray());
        Assert.Equal(business.Email, row.GetProperty("ownerEmail").GetString());
        Assert.Equal(1, row.GetProperty("serviceCount").GetInt32());

        // Los comodines de LIKE se tratan como texto, no como "todo".
        var wildcard = await ApiFactory.Json(await admin.GetAsync("/api/admin/tenants?search=%25%25%25"));
        Assert.Equal(0, wildcard.GetArrayLength());

        var stats = await ApiFactory.Json(await admin.GetAsync("/api/admin/stats"));
        Assert.True(stats.GetProperty("businesses").GetInt32() >= 1);
    }

    [Fact]
    public async Task Public_page_shows_only_active_services_and_public_fields()
    {
        var business = await factory.RegisterBusinessAsync("Barbería Pública");
        await ApiFactory.CreateServiceAsync(business.Client, "Corte clásico");
        await ApiFactory.CreateServiceAsync(business.Client, "Servicio oculto", isActive: false);
        var other = await factory.RegisterBusinessAsync();
        await ApiFactory.CreateServiceAsync(other.Client, "De otro negocio");

        var response = await factory.NewClient().GetAsync($"/api/public/businesses/{business.Slug.ToUpperInvariant()}");
        await ApiFactory.Expect(response, HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        var page = await ApiFactory.Json(response);

        Assert.Equal("Barbería Pública", page.GetProperty("name").GetString());
        var service = Assert.Single(page.GetProperty("services").EnumerateArray());
        Assert.Equal("Corte clásico", service.GetProperty("name").GetString());
        Assert.DoesNotContain("isActive", raw);
        Assert.DoesNotContain("tenantId", raw);
        Assert.DoesNotContain(business.Email, raw);

        await ApiFactory.Expect(await factory.NewClient().GetAsync("/api/public/businesses/no-existe-este"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.NewClient().GetAsync("/api/business");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("default-src 'none'; frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
