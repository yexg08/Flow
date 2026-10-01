using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Flow.Tests.Integration;

[Collection(ApiCollection.Name)]
public class TeamTests(ApiFactory factory)
{
    private static object Staff(string name, params Guid[] serviceIds) =>
        new { name, color = "#22c55e", isActive = true, serviceIds };

    private static async Task<Guid> CreateStaffAsync(HttpClient owner, string name, params Guid[] serviceIds)
    {
        var response = await owner.PostAsJsonAsync("/api/staff", Staff(name, serviceIds));
        await ApiFactory.Expect(response, HttpStatusCode.Created);
        return (await ApiFactory.Json(response)).GetProperty("id").GetGuid();
    }

    private static object Range(string day, string start, string end) => new { dayOfWeek = day, start, end };

    [Fact]
    public async Task Owner_builds_the_team_with_its_services()
    {
        var business = await factory.RegisterBusinessAsync();
        var haircut = await ApiFactory.CreateServiceAsync(business.Client, "Corte");
        var beard = await ApiFactory.CreateServiceAsync(business.Client, "Barba");

        var id = await CreateStaffAsync(business.Client, "Andrés", haircut, beard);
        var staff = await ApiFactory.Json(await business.Client.GetAsync($"/api/staff/{id}"));
        Assert.Equal("Andrés", staff.GetProperty("name").GetString());
        Assert.Equal(new[] { haircut, beard }.Order(), staff.GetProperty("serviceIds").EnumerateArray().Select(e => e.GetGuid()).Order());

        // Quitar un servicio y dejar solo el otro.
        var update = await business.Client.PutAsJsonAsync($"/api/staff/{id}", Staff("Andrés M.", beard));
        await ApiFactory.Expect(update, HttpStatusCode.OK);
        var updated = await ApiFactory.Json(update);
        Assert.Equal("Andrés M.", updated.GetProperty("name").GetString());
        Assert.Equal(beard, Assert.Single(updated.GetProperty("serviceIds").EnumerateArray()).GetGuid());
    }

    [Fact]
    public async Task Team_is_isolated_between_businesses()
    {
        var a = await factory.RegisterBusinessAsync();
        var b = await factory.RegisterBusinessAsync();
        var serviceOfA = await ApiFactory.CreateServiceAsync(a.Client);
        var staffOfA = await CreateStaffAsync(a.Client, "Persona de A", serviceOfA);

        Assert.Equal(0, (await ApiFactory.Json(await b.Client.GetAsync("/api/staff"))).GetArrayLength());
        await ApiFactory.Expect(await b.Client.GetAsync($"/api/staff/{staffOfA}"), HttpStatusCode.NotFound);
        await ApiFactory.Expect(await b.Client.PutAsJsonAsync($"/api/staff/{staffOfA}", Staff("Hackeado")), HttpStatusCode.NotFound);
        await ApiFactory.Expect(
            await b.Client.PutAsJsonAsync($"/api/staff/{staffOfA}/schedule", new { ranges = new[] { Range("Monday", "08:00", "12:00") } }),
            HttpStatusCode.NotFound);
        await ApiFactory.Expect(await b.Client.DeleteAsync($"/api/staff/{staffOfA}"), HttpStatusCode.NotFound);

        // B no puede asignarle a su gente un servicio de A, aunque conozca el id.
        var response = await b.Client.PostAsJsonAsync("/api/staff", Staff("Persona de B", serviceOfA));
        await ApiFactory.Expect(response, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(response)).GetProperty("errors").TryGetProperty("serviceIds", out _));
    }

    [Fact]
    public async Task Weekly_schedule_is_replaced_and_validated()
    {
        var business = await factory.RegisterBusinessAsync();
        var id = await CreateStaffAsync(business.Client, "Laura");
        var url = $"/api/staff/{id}/schedule";

        var response = await business.Client.PutAsJsonAsync(url, new
        {
            ranges = new[]
            {
                Range("Monday", "14:00", "18:00"),
                Range("Monday", "08:00", "12:00"),
                Range("Saturday", "09:00", "13:00")
            }
        });
        await ApiFactory.Expect(response, HttpStatusCode.OK);
        var hours = (await ApiFactory.Json(response)).GetProperty("workingHours").EnumerateArray()
            .Select(h => $"{h.GetProperty("dayOfWeek").GetString()} {h.GetProperty("start").GetString()}-{h.GetProperty("end").GetString()}")
            .ToList();
        Assert.Equal(["Monday 08:00-12:00", "Monday 14:00-18:00", "Saturday 09:00-13:00"], hours);

        // Reemplazar deja solo el horario nuevo.
        var replaced = await business.Client.PutAsJsonAsync(url, new { ranges = new[] { Range("Tuesday", "10:00", "16:00") } });
        await ApiFactory.Expect(replaced, HttpStatusCode.OK);
        var only = Assert.Single((await ApiFactory.Json(replaced)).GetProperty("workingHours").EnumerateArray());
        Assert.Equal("Tuesday", only.GetProperty("dayOfWeek").GetString());
        var rows = await factory.WithDbAsync(business.TenantId, (db, _) => db.WorkingHours.CountAsync(w => w.StaffMemberId == id));
        Assert.Equal(1, rows);

        // Reglas: tramos que se cruzan, cierre antes de apertura, formato y pasos de 5 minutos.
        foreach (var invalid in new[]
        {
            new[] { Range("Monday", "08:00", "12:00"), Range("Monday", "11:00", "14:00") },
            new[] { Range("Monday", "12:00", "08:00") },
            new[] { Range("Monday", "8am", "12:00") },
            new[] { Range("Monday", "08:03", "12:00") }
        })
        {
            var bad = await business.Client.PutAsJsonAsync(url, new { ranges = invalid });
            await ApiFactory.Expect(bad, HttpStatusCode.BadRequest);
            Assert.True((await ApiFactory.Json(bad)).GetProperty("errors").TryGetProperty("ranges", out _));
        }

        // Tramos que se tocan sin cruzarse sí valen.
        await ApiFactory.Expect(
            await business.Client.PutAsJsonAsync(url, new { ranges = new[] { Range("Friday", "08:00", "12:00"), Range("Friday", "12:00", "13:00") } }),
            HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deleting_a_service_or_a_person_cleans_up_what_depends_on_them()
    {
        var business = await factory.RegisterBusinessAsync();
        var service = await ApiFactory.CreateServiceAsync(business.Client);
        var id = await CreateStaffAsync(business.Client, "Camila", service);
        await business.Client.PutAsJsonAsync($"/api/staff/{id}/schedule", new { ranges = new[] { Range("Monday", "08:00", "12:00") } });
        await business.Client.PostAsJsonAsync("/api/time-off",
            new { staffMemberId = id, startsAt = "2099-01-10T08:00", endsAt = "2099-01-10T12:00", reason = "Cita médica" });

        await ApiFactory.Expect(await business.Client.DeleteAsync($"/api/services/{service}"), HttpStatusCode.NoContent);
        var staff = await ApiFactory.Json(await business.Client.GetAsync($"/api/staff/{id}"));
        Assert.Equal(0, staff.GetProperty("serviceIds").GetArrayLength());

        await ApiFactory.Expect(await business.Client.DeleteAsync($"/api/staff/{id}"), HttpStatusCode.NoContent);
        var leftovers = await factory.WithDbAsync(business.TenantId, async (db, _) =>
            await db.WorkingHours.CountAsync(w => w.StaffMemberId == id) + await db.TimeOff.CountAsync(t => t.StaffMemberId == id));
        Assert.Equal(0, leftovers);
    }

    [Fact]
    public async Task Staff_cannot_change_the_team()
    {
        var business = await factory.RegisterBusinessAsync();
        var id = await CreateStaffAsync(business.Client, "Daniel");
        var staffClient = await factory.CreateStaffAccountAsync(business.TenantId);

        await ApiFactory.Expect(await staffClient.GetAsync("/api/staff"), HttpStatusCode.OK);
        await ApiFactory.Expect(await staffClient.PutAsJsonAsync($"/api/staff/{id}", Staff("Cambiado")), HttpStatusCode.Forbidden);
        await ApiFactory.Expect(
            await staffClient.PutAsJsonAsync($"/api/staff/{id}/schedule", new { ranges = Array.Empty<object>() }), HttpStatusCode.Forbidden);
    }
}

[Collection(ApiCollection.Name)]
public class TimeOffTests(ApiFactory factory)
{
    private static object Block(Guid? staffMemberId, string startsAt, string endsAt, string? reason = null) =>
        new { staffMemberId, startsAt, endsAt, reason };

    [Fact]
    public async Task Blocks_are_entered_in_business_time_and_stored_in_utc()
    {
        var business = await factory.RegisterBusinessAsync();
        var response = await business.Client.PostAsJsonAsync("/api/time-off", Block(null, "2099-12-25T00:00", "2099-12-26T00:00", "Navidad"));
        await ApiFactory.Expect(response, HttpStatusCode.Created);
        var body = await ApiFactory.Json(response);
        var id = body.GetProperty("id").GetGuid();

        // La API devuelve la hora local del negocio, tal como se escribió...
        Assert.Equal("2099-12-25T00:00:00", body.GetProperty("startsAt").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("staffName").ValueKind);

        // ...y la base la guarda en UTC: Bogotá es UTC-5.
        var stored = await factory.WithDbAsync(business.TenantId, (db, _) => db.TimeOff.SingleAsync(t => t.Id == id));
        Assert.Equal(new DateTime(2099, 12, 25, 5, 0, 0, DateTimeKind.Utc), stored.StartsAtUtc);
    }

    [Fact]
    public async Task Only_upcoming_blocks_are_listed_with_the_person_name()
    {
        var business = await factory.RegisterBusinessAsync();
        var staff = await business.Client.PostAsJsonAsync("/api/staff", new { name = "Sara", color = "#ec4899", isActive = true, serviceIds = Array.Empty<Guid>() });
        var staffId = (await ApiFactory.Json(staff)).GetProperty("id").GetGuid();

        await business.Client.PostAsJsonAsync("/api/time-off", Block(null, "2020-01-01T00:00", "2020-01-02T00:00", "Ya pasó"));
        await business.Client.PostAsJsonAsync("/api/time-off", Block(staffId, "2099-03-02T08:00", "2099-03-06T18:00", "Vacaciones"));
        await business.Client.PostAsJsonAsync("/api/time-off", Block(null, "2099-01-01T00:00", "2099-01-02T00:00", "Año nuevo"));

        var list = (await ApiFactory.Json(await business.Client.GetAsync("/api/time-off"))).EnumerateArray().ToList();
        Assert.Equal(["Año nuevo", "Vacaciones"], list.Select(t => t.GetProperty("reason").GetString()));
        Assert.Equal("Sara", list[1].GetProperty("staffName").GetString());
    }

    [Fact]
    public async Task Blocks_are_isolated_between_businesses()
    {
        var a = await factory.RegisterBusinessAsync();
        var b = await factory.RegisterBusinessAsync();
        var staffOfA = (await ApiFactory.Json(await a.Client.PostAsJsonAsync("/api/staff",
            new { name = "De A", color = "#ec4899", isActive = true, serviceIds = Array.Empty<Guid>() }))).GetProperty("id").GetGuid();
        var blockOfA = (await ApiFactory.Json(await a.Client.PostAsJsonAsync("/api/time-off",
            Block(null, "2099-05-01T00:00", "2099-05-02T00:00")))).GetProperty("id").GetGuid();

        Assert.Equal(0, (await ApiFactory.Json(await b.Client.GetAsync("/api/time-off"))).GetArrayLength());
        await ApiFactory.Expect(await b.Client.DeleteAsync($"/api/time-off/{blockOfA}"), HttpStatusCode.NotFound);

        var response = await b.Client.PostAsJsonAsync("/api/time-off", Block(staffOfA, "2099-05-01T08:00", "2099-05-01T12:00"));
        await ApiFactory.Expect(response, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(response)).GetProperty("errors").TryGetProperty("staffMemberId", out _));
    }

    [Fact]
    public async Task Invalid_ranges_and_nonexistent_local_times_are_rejected()
    {
        var business = await factory.RegisterBusinessAsync();

        var backwards = await business.Client.PostAsJsonAsync("/api/time-off", Block(null, "2099-05-01T12:00", "2099-05-01T08:00"));
        await ApiFactory.Expect(backwards, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(backwards)).GetProperty("errors").TryGetProperty("endsAt", out _));

        // En Madrid, el 28 de marzo de 2027 el reloj salta de 02:00 a 03:00: las 02:30 no existen.
        await ApiFactory.Expect(await business.Client.PutAsJsonAsync("/api/business", new
        {
            name = "Negocio en Madrid", slug = business.Slug, timeZone = "Europe/Madrid", whatsApp = (string?)null, accentColor = "#a855f7"
        }), HttpStatusCode.OK);
        var gap = await business.Client.PostAsJsonAsync("/api/time-off", Block(null, "2027-03-28T02:30", "2027-03-28T05:00"));
        await ApiFactory.Expect(gap, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(gap)).GetProperty("errors").TryGetProperty("startsAt", out _));
    }
}
