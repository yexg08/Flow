using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Flow.Tests.Integration;

/// <summary>
/// la API puede servir el frontend compilado (asi se usa en la imagen de Docker) sin tocar el comportamiento de /api
/// </summary>
[Collection(ApiCollection.Name)]
public class FrontendHostingTests(ApiFactory factory) : IDisposable
{
    private const string ThemeScript = "window.__theme = 'dark';";
    private readonly string _webRoot = Directory.CreateTempSubdirectory("flow-wwwroot-").FullName;

    public void Dispose() => Directory.Delete(_webRoot, recursive: true);

    private WebApplicationFactory<Program> FactoryWithFrontend()
    {
        File.WriteAllText(Path.Combine(_webRoot, "index.html"),
            $"<!doctype html><html><head><script>{ThemeScript}</script></head><body><app-root></app-root></body></html>");
        File.WriteAllText(Path.Combine(_webRoot, "main-ABC123.js"), "console.log(1);");
        return factory.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));
    }

    private static HttpClient NewClient(WebApplicationFactory<Program> frontend) =>
        frontend.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    // cliente con la sesion de un negocio recien registrado (la clave de los tokens es la misma en las dos fabricas)
    private async Task<HttpClient> NewSignedInClientAsync(WebApplicationFactory<Program> frontend)
    {
        var business = await factory.RegisterBusinessAsync();
        var client = NewClient(frontend);
        client.DefaultRequestHeaders.Authorization = business.Client.DefaultRequestHeaders.Authorization;
        return client;
    }

    [Fact]
    public async Task Front_pages_are_served_without_a_session_with_a_policy_that_allows_only_their_own_scripts()
    {
        var client = NewClient(FactoryWithFrontend());

        foreach (var path in new[] { "/", "/app/agenda", "/n/mi-negocio/reservar/abc", "/cita/AbC-123_xyz" })
        {
            var response = await client.GetAsync(path);
            await ApiFactory.Expect(response, HttpStatusCode.OK);
            Assert.Contains("<app-root>", await response.Content.ReadAsStringAsync());
            Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());

            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(ThemeScript)));
            var policy = response.Headers.GetValues("Content-Security-Policy").Single();
            Assert.Contains($"script-src 'self' 'sha256-{hash}'", policy);
            Assert.DoesNotContain("script-src 'self' 'unsafe-inline'", policy);
        }
    }

    [Fact]
    public async Task Assets_with_a_hash_in_the_name_are_cached_and_missing_files_are_not_found()
    {
        var frontend = FactoryWithFrontend();
        var anonymous = NewClient(frontend);

        var asset = await anonymous.GetAsync("/main-ABC123.js");
        await ApiFactory.Expect(asset, HttpStatusCode.OK);
        Assert.Contains("immutable", asset.Headers.CacheControl!.ToString());

        // una ruta que no existe se deniega sin sesion (denegar por defecto) y da 404 con sesion
        await ApiFactory.Expect(await anonymous.GetAsync("/no-existe.js"), HttpStatusCode.Unauthorized);
        var signedIn = await NewSignedInClientAsync(frontend);
        await ApiFactory.Expect(await signedIn.GetAsync("/no-existe.js"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unknown_api_routes_never_return_the_frontend()
    {
        var frontend = FactoryWithFrontend();
        var anonymous = NewClient(frontend);
        var signedIn = await NewSignedInClientAsync(frontend);

        var denied = await anonymous.GetAsync("/api/no-existe");
        var missing = await signedIn.GetAsync("/api/no-existe");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        foreach (var response in new[] { denied, missing })
        {
            Assert.DoesNotContain("<app-root>", await response.Content.ReadAsStringAsync());
            Assert.Equal("default-src 'none'; frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        }
    }

    [Fact]
    public async Task Health_endpoint_answers_without_a_session()
    {
        var response = await factory.NewClient().GetAsync("/api/health");
        await ApiFactory.Expect(response, HttpStatusCode.OK);
        Assert.Equal("ok", (await ApiFactory.Json(response)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Without_a_compiled_frontend_the_root_is_not_served()
    {
        var response = await factory.NewClient().GetAsync("/");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound);
        Assert.DoesNotContain("<app-root>", await response.Content.ReadAsStringAsync());
    }
}
