using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Flow.Api.Auth;
using Flow.Application.Common;
using Flow.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Flow.Tests.Integration;

/// <summary>
/// "Denegar por defecto", verificado recorriendo TODOS los endpoints de la API (incluidos los que se agreguen en el
/// futuro): cada rol solo entra a lo suyo.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthorizationTests(ApiFactory factory)
{
    private sealed record ApiEndpoint(string Method, string Url, string Kind);

    private List<ApiEndpoint> AllEndpoints()
    {
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
        var result = new List<ApiEndpoint>();

        foreach (var endpoint in endpoints)
        {
            var path = endpoint.RoutePattern.RawText!;
            if (!path.StartsWith("api/")) continue;

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            var authorize = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
            var kind =
                endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ? "anonymous"
                : authorize.Any(a => a.Policy == Policies.SuperAdmin) ? "superadmin"
                : authorize.Any(a => a.Policy == Policies.TenantOwner) ? "owner"
                : authorize.Count > 0 && authorize.All(a => a.Policy is null && a.Roles is null) ? "session"
                : "member"; // sin atributos → política de respaldo

            // Los parámetros de ruta se reemplazan por un Guid: la autorización se evalúa antes de buscar nada.
            var url = "/" + Regex.Replace(path, @"\{[^}]+\}", Guid.NewGuid().ToString());
            result.AddRange(methods.Select(method => new ApiEndpoint(method, url, kind)));
        }

        Assert.NotEmpty(result);
        return result;
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, ApiEndpoint endpoint)
    {
        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), endpoint.Url);
        if (endpoint.Method is "POST" or "PUT" or "PATCH") request.Content = JsonContent.Create(new { });
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Without_session_every_non_public_endpoint_returns_401()
    {
        var client = factory.NewClient();
        foreach (var endpoint in AllEndpoints().Where(e => e.Kind != "anonymous"))
        {
            var response = await SendAsync(client, endpoint);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{endpoint.Method} {endpoint.Url} devolvió {(int)response.StatusCode} sin sesión.");
        }
    }

    [Fact]
    public async Task Superadmin_cannot_use_business_endpoints()
    {
        var admin = await factory.LoginAsSuperAdminAsync();
        foreach (var endpoint in AllEndpoints().Where(e => e.Kind is "member" or "owner"))
        {
            var response = await SendAsync(admin, endpoint);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                $"El superadmin recibió {(int)response.StatusCode} en {endpoint.Method} {endpoint.Url}.");
        }
    }

    [Fact]
    public async Task Business_accounts_cannot_use_superadmin_endpoints()
    {
        var owner = (await factory.RegisterBusinessAsync()).Client;
        foreach (var endpoint in AllEndpoints().Where(e => e.Kind == "superadmin"))
        {
            var response = await SendAsync(owner, endpoint);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                $"Un dueño recibió {(int)response.StatusCode} en {endpoint.Method} {endpoint.Url}.");
        }
    }

    [Fact]
    public async Task Staff_can_read_but_only_the_owner_manages_the_business()
    {
        var business = await factory.RegisterBusinessAsync();
        var serviceId = await ApiFactory.CreateServiceAsync(business.Client);
        var staff = await factory.CreateStaffAccountAsync(business.TenantId);

        await ApiFactory.Expect(await staff.GetAsync("/api/services"), HttpStatusCode.OK);
        await ApiFactory.Expect(await staff.GetAsync("/api/business"), HttpStatusCode.OK);

        foreach (var endpoint in AllEndpoints().Where(e => e.Kind == "owner"))
        {
            var response = await SendAsync(staff, endpoint with { Url = endpoint.Url });
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                $"Un empleado recibió {(int)response.StatusCode} en {endpoint.Method} {endpoint.Url}.");
        }

        await ApiFactory.Expect(await staff.DeleteAsync($"/api/services/{serviceId}"), HttpStatusCode.Forbidden);
    }
}
