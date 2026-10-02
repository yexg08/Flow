using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Flow.Tests.Integration;

[Collection(ApiCollection.Name)]
public class TeamAccountsTests(ApiFactory factory)
{
    private const string NewPassword = "MiClaveNueva2026";

    private static async Task<Guid> CreateStaffAsync(HttpClient owner, string name = "Ana")
    {
        var response = await owner.PostAsJsonAsync("/api/staff", new { name, color = "#ec4899", isActive = true, serviceIds = Array.Empty<Guid>() });
        await ApiFactory.Expect(response, HttpStatusCode.Created);
        return (await ApiFactory.Json(response)).GetProperty("id").GetGuid();
    }

    private static string NewEmail() => $"equipo.{Guid.NewGuid():N}@flow.test";

    private static async Task<string> GrantAsync(HttpClient owner, Guid staffId, string email)
    {
        var response = await owner.PostAsJsonAsync($"/api/staff/{staffId}/account", new { email });
        await ApiFactory.Expect(response, HttpStatusCode.Created);
        return (await ApiFactory.Json(response)).GetProperty("temporaryPassword").GetString()!;
    }

    private async Task<(HttpClient Client, JsonElement Body)> LoginRawAsync(string email, string password)
    {
        var client = factory.NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        await ApiFactory.Expect(response, HttpStatusCode.OK);
        var body = await ApiFactory.Json(response);
        ApiFactory.Authorize(client, body);
        return (client, body);
    }

    private static async Task ChangePasswordAsync(HttpClient client, string current, string next)
    {
        var response = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = current, newPassword = next });
        await ApiFactory.Expect(response, HttpStatusCode.OK);
        ApiFactory.Authorize(client, await ApiFactory.Json(response));
    }

    [Fact]
    public async Task Owner_gives_access_with_a_temporary_password_that_must_be_changed()
    {
        var business = await factory.RegisterBusinessAsync();
        var staffId = await CreateStaffAsync(business.Client);
        var email = NewEmail();
        var temporary = await GrantAsync(business.Client, staffId, email);
        Assert.Matches("^[A-Za-z2-9]{4}-[A-Za-z2-9]{4}-[A-Za-z2-9]{4}-[A-Za-z2-9]{4}$", temporary);

        var (client, body) = await LoginRawAsync(email, temporary);
        Assert.True(body.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(staffId, body.GetProperty("user").GetProperty("staffMemberId").GetGuid());
        Assert.Equal("Staff", body.GetProperty("user").GetProperty("role").GetString());

        // Con la contraseña temporal solo puede ver su sesión y cambiarla.
        await ApiFactory.Expect(await client.GetAsync("/api/staff"), HttpStatusCode.Forbidden);
        await ApiFactory.Expect(await client.GetAsync("/api/auth/me"), HttpStatusCode.OK);

        var wrongCurrent = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "NoEsEsta123", newPassword = NewPassword });
        await ApiFactory.Expect(wrongCurrent, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(wrongCurrent)).GetProperty("errors").TryGetProperty("currentPassword", out _));

        await ChangePasswordAsync(client, temporary, NewPassword);
        await ApiFactory.Expect(await client.GetAsync("/api/staff"), HttpStatusCode.OK);
        var me = await ApiFactory.Json(await client.GetAsync("/api/auth/me"));
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());

        // El dueño ve que ya entró; el empleado no puede gestionar cuentas.
        var account = Assert.Single((await ApiFactory.Json(await business.Client.GetAsync("/api/staff/accounts"))).EnumerateArray());
        Assert.False(account.GetProperty("pendingFirstLogin").GetBoolean());
        Assert.Equal(email, account.GetProperty("email").GetString());
        await ApiFactory.Expect(await client.GetAsync("/api/staff/accounts"), HttpStatusCode.Forbidden);
        await ApiFactory.Expect(await client.PostAsJsonAsync($"/api/staff/{staffId}/account", new { email = NewEmail() }), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Resetting_or_revoking_cuts_access_immediately()
    {
        var business = await factory.RegisterBusinessAsync();
        var staffId = await CreateStaffAsync(business.Client);
        var email = NewEmail();
        var temporary = await GrantAsync(business.Client, staffId, email);
        var (session, _) = await LoginRawAsync(email, temporary);
        await ChangePasswordAsync(session, temporary, NewPassword);

        // Contraseña temporal nueva: la anterior ya no sirve y la sesión abierta se corta.
        var reset = await business.Client.PostAsync($"/api/staff/{staffId}/account/reset-password", null);
        await ApiFactory.Expect(reset, HttpStatusCode.OK);
        var newTemporary = (await ApiFactory.Json(reset)).GetProperty("temporaryPassword").GetString()!;
        await ApiFactory.Expect(await session.GetAsync("/api/staff"), HttpStatusCode.Unauthorized);
        await ApiFactory.Expect(await factory.NewClient().PostAsJsonAsync("/api/auth/login", new { email, password = NewPassword }), HttpStatusCode.Unauthorized);
        var (again, body) = await LoginRawAsync(email, newTemporary);
        Assert.True(body.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());

        // Quitar el acceso borra la cuenta: ni la sesión ni el login sirven, y se puede volver a dar con el mismo correo.
        await ApiFactory.Expect(await business.Client.DeleteAsync($"/api/staff/{staffId}/account"), HttpStatusCode.NoContent);
        await ApiFactory.Expect(await again.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
        await ApiFactory.Expect(await factory.NewClient().PostAsJsonAsync("/api/auth/login", new { email, password = newTemporary }), HttpStatusCode.Unauthorized);
        await ApiFactory.Expect(await business.Client.GetAsync($"/api/staff/{staffId}"), HttpStatusCode.OK);
        await GrantAsync(business.Client, staffId, email);
    }

    [Fact]
    public async Task Expired_temporary_passwords_do_not_work()
    {
        var business = await factory.RegisterBusinessAsync();
        var staffId = await CreateStaffAsync(business.Client);
        var email = NewEmail();
        var temporary = await GrantAsync(business.Client, staffId, email);

        await factory.WithDbAsync(business.TenantId, async (db, _) =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            user.TemporaryPasswordExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });

        var login = await factory.NewClient().PostAsJsonAsync("/api/auth/login", new { email, password = temporary });
        await ApiFactory.Expect(login, HttpStatusCode.Unauthorized);
        Assert.Contains("venció", (await ApiFactory.Json(login)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Accounts_follow_the_business_rules()
    {
        var a = await factory.RegisterBusinessAsync();
        var b = await factory.RegisterBusinessAsync();
        var staffOfA = await CreateStaffAsync(a.Client);

        // Otro negocio no puede dar acceso a la gente de A.
        await ApiFactory.Expect(await b.Client.PostAsJsonAsync($"/api/staff/{staffOfA}/account", new { email = NewEmail() }), HttpStatusCode.NotFound);
        await ApiFactory.Expect(await b.Client.DeleteAsync($"/api/staff/{staffOfA}/account"), HttpStatusCode.NotFound);

        // Un correo que ya tiene cuenta (aquí, el del dueño de B) no se puede reutilizar.
        var taken = await a.Client.PostAsJsonAsync($"/api/staff/{staffOfA}/account", new { email = b.Email });
        await ApiFactory.Expect(taken, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(taken)).GetProperty("errors").TryGetProperty("email", out _));

        await GrantAsync(a.Client, staffOfA, NewEmail());
        await ApiFactory.Expect(await a.Client.PostAsJsonAsync($"/api/staff/{staffOfA}/account", new { email = NewEmail() }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deleting_a_person_deletes_the_account_but_only_if_the_person_can_be_deleted()
    {
        var business = await factory.RegisterBusinessAsync();
        var serviceId = await ApiFactory.CreateServiceAsync(business.Client);

        // Con citas: no se borra la persona y su cuenta sigue funcionando.
        var busy = (await ApiFactory.Json(await business.Client.PostAsJsonAsync("/api/staff",
            new { name = "Con citas", color = "#22c55e", isActive = true, serviceIds = new[] { serviceId } }))).GetProperty("id").GetGuid();
        var busyEmail = NewEmail();
        var busyPassword = await GrantAsync(business.Client, busy, busyEmail);
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2).ToString("yyyy-MM-dd");
        await ApiFactory.Expect(await business.Client.PostAsJsonAsync("/api/appointments", new
        {
            serviceId, staffMemberId = busy, date = tomorrow, time = "10:00", customerId = (Guid?)null,
            customerName = "Cliente", customerPhone = "3001112233", note = (string?)null
        }), HttpStatusCode.Created);
        await ApiFactory.Expect(await business.Client.DeleteAsync($"/api/staff/{busy}"), HttpStatusCode.Conflict);
        await LoginRawAsync(busyEmail, busyPassword);

        // Sin citas: se borra la persona y también su cuenta.
        var free = await CreateStaffAsync(business.Client, "Sin citas");
        var freeEmail = NewEmail();
        var freePassword = await GrantAsync(business.Client, free, freeEmail);
        await ApiFactory.Expect(await business.Client.DeleteAsync($"/api/staff/{free}"), HttpStatusCode.NoContent);
        await ApiFactory.Expect(await factory.NewClient().PostAsJsonAsync("/api/auth/login", new { email = freeEmail, password = freePassword }), HttpStatusCode.Unauthorized);
    }
}
