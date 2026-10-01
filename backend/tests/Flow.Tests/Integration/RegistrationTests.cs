using System.Net;
using System.Net.Http.Json;

namespace Flow.Tests.Integration;

[Collection(ApiCollection.Name)]
public class RegistrationTests(ApiFactory factory)
{
    private static object Register(string slug, string email, string password = ApiFactory.OwnerPassword) =>
        new { businessName = "Barbería Prueba", slug, fullName = "Ana Gómez", email, password };

    [Fact]
    public async Task Register_creates_the_business_its_owner_and_a_session()
    {
        var client = factory.NewClient();
        var slug = $"barberia-{Guid.NewGuid():N}"[..30];
        var response = await client.PostAsJsonAsync("/api/auth/register", Register(slug, $"{slug}@flow.test"));
        await ApiFactory.Expect(response, HttpStatusCode.Created);

        var body = await ApiFactory.Json(response);
        Assert.Equal("Owner", body.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal(slug, body.GetProperty("user").GetProperty("tenant").GetProperty("slug").GetString());

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("flow_refresh="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);

        ApiFactory.Authorize(client, body);
        var settings = await ApiFactory.Json(await client.GetAsync("/api/business"));
        Assert.Equal("Barbería Prueba", settings.GetProperty("name").GetString());
        Assert.Equal("America/Bogota", settings.GetProperty("timeZone").GetString());

        // La cookie de refresh renueva la sesión.
        await ApiFactory.Expect(await client.PostAsync("/api/auth/refresh", null), HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Mi Negocio")]
    [InlineData("ab")]
    [InlineData("-guion-")]
    [InlineData("peluquería")]
    public async Task Invalid_or_reserved_slugs_are_rejected(string slug)
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Register(slug, $"x{Guid.NewGuid():N}@flow.test"));
        await ApiFactory.Expect(response, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(response)).GetProperty("errors").TryGetProperty("slug", out _));
    }

    [Fact]
    public async Task Slug_and_email_must_be_unique()
    {
        var existing = await factory.RegisterBusinessAsync();

        var sameSlug = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Register(existing.Slug, $"otro.{Guid.NewGuid():N}@flow.test"));
        await ApiFactory.Expect(sameSlug, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(sameSlug)).GetProperty("errors").TryGetProperty("slug", out _));

        var sameEmail = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Register($"libre-{Guid.NewGuid():N}"[..20], existing.Email));
        await ApiFactory.Expect(sameEmail, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(sameEmail)).GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Weak_passwords_are_rejected()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Register($"debil-{Guid.NewGuid():N}"[..20], $"d{Guid.NewGuid():N}@flow.test", "solo-letras-largas"));
        await ApiFactory.Expect(response, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(response)).GetProperty("errors").TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Slug_availability_reports_taken_reserved_and_free_slugs()
    {
        var existing = await factory.RegisterBusinessAsync();
        var client = factory.NewClient();

        async Task<bool> Available(string slug) =>
            (await ApiFactory.Json(await client.GetAsync($"/api/auth/slug-availability?slug={slug}"))).GetProperty("available").GetBoolean();

        Assert.False(await Available(existing.Slug));
        Assert.False(await Available("admin"));
        Assert.False(await Available("Con%20Espacios"));
        Assert.True(await Available($"libre-{Guid.NewGuid():N}"[..20]));
    }

    [Fact]
    public async Task Login_gives_the_same_answer_for_wrong_password_and_unknown_email()
    {
        var existing = await factory.RegisterBusinessAsync();
        var client = factory.NewClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { email = existing.Email, password = "Incorrecta123" });
        var unknownEmail = await client.PostAsJsonAsync("/api/auth/login", new { email = "nadie@flow.test", password = "Incorrecta123" });

        await ApiFactory.Expect(wrongPassword, HttpStatusCode.Unauthorized);
        await ApiFactory.Expect(unknownEmail, HttpStatusCode.Unauthorized);
        Assert.Equal(
            (await ApiFactory.Json(wrongPassword)).GetProperty("detail").GetString(),
            (await ApiFactory.Json(unknownEmail)).GetProperty("detail").GetString());
    }
}
