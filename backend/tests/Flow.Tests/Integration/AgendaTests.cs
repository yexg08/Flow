using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Flow.Tests.Integration;

[Collection(ApiCollection.Name)]
public class AgendaTests(ApiFactory factory)
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Bogota));
    private static DateOnly Tomorrow => Today.AddDays(1);
    private static DateOnly Yesterday => Today.AddDays(-1);

    private static string NewPhone() => "3" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    private sealed record Setup(ApiFactory.Business Business, Guid ServiceId, Guid StaffId, Guid OtherStaffId);

    /// <summary>Servicio de 30 minutos y dos personas que lo hacen, con horario todos los días de 08:00 a 18:00.</summary>
    private async Task<Setup> SetupAsync()
    {
        var business = await factory.RegisterBusinessAsync("Peluquería con agenda");
        var serviceId = await ApiFactory.CreateServiceAsync(business.Client, "Corte");
        var staff = new List<Guid>();
        foreach (var name in new[] { "Ana", "Beto" })
        {
            var response = await business.Client.PostAsJsonAsync("/api/staff",
                new { name, color = "#ec4899", isActive = true, serviceIds = new[] { serviceId } });
            await ApiFactory.Expect(response, HttpStatusCode.Created);
            var id = (await ApiFactory.Json(response)).GetProperty("id").GetGuid();
            var week = Enum.GetNames<DayOfWeek>().Select(d => new { dayOfWeek = d, start = "08:00", end = "18:00" });
            await business.Client.PutAsJsonAsync($"/api/staff/{id}/schedule", new { ranges = week });
            staff.Add(id);
        }
        return new Setup(business, serviceId, staff[0], staff[1]);
    }

    private static object PanelBooking(Setup s, DateOnly date, string time, Guid? staffId = null, Guid? customerId = null, string? phone = null) => new
    {
        serviceId = s.ServiceId, staffMemberId = staffId ?? s.StaffId, date = date.ToString("yyyy-MM-dd"), time,
        customerId, customerName = customerId is null ? "Carlos Pérez" : null, customerPhone = customerId is null ? phone ?? NewPhone() : null,
        note = (string?)null
    };

    private static async Task<JsonElement> CreateAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/appointments", body);
        await ApiFactory.Expect(response, HttpStatusCode.Created);
        return await ApiFactory.Json(response);
    }

    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient client, Guid id, string status) =>
        client.PatchAsJsonAsync($"/api/appointments/{id}/status", new { status });

    [Fact]
    public async Task Panel_books_for_new_and_known_customers_and_the_agenda_shows_it()
    {
        var s = await SetupAsync();
        var client = s.Business.Client;

        // Fuera del horario del equipo (07:00) también se puede: lo decide el negocio.
        var first = await CreateAsync(client, PanelBooking(s, Tomorrow, "07:00"));
        Assert.Equal($"{Tomorrow:yyyy-MM-dd}T07:00:00", first.GetProperty("startsAt").GetString());
        Assert.Equal("Confirmed", first.GetProperty("status").GetString());
        var customerId = first.GetProperty("customerId").GetGuid();

        var second = await CreateAsync(client, PanelBooking(s, Tomorrow, "07:30", customerId: customerId));
        Assert.Equal(customerId, second.GetProperty("customerId").GetGuid());

        var agenda = await ApiFactory.Json(await client.GetAsync($"/api/appointments/agenda?from={Tomorrow:yyyy-MM-dd}&to={Tomorrow:yyyy-MM-dd}"));
        Assert.Equal(2, agenda.GetProperty("appointments").GetArrayLength());

        await ApiFactory.Expect(
            await client.GetAsync($"/api/appointments/agenda?from={Today:yyyy-MM-dd}&to={Today.AddDays(60):yyyy-MM-dd}"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Panel_bookings_cannot_overlap_appointments_or_time_off()
    {
        var s = await SetupAsync();
        var client = s.Business.Client;
        await CreateAsync(client, PanelBooking(s, Tomorrow, "10:00"));

        await ApiFactory.Expect(await client.PostAsJsonAsync("/api/appointments", PanelBooking(s, Tomorrow, "10:15")), HttpStatusCode.Conflict);
        // Otra persona sí puede a esa hora.
        await CreateAsync(client, PanelBooking(s, Tomorrow, "10:15", staffId: s.OtherStaffId));

        await client.PostAsJsonAsync("/api/time-off",
            new { staffMemberId = s.StaffId, startsAt = $"{Tomorrow:yyyy-MM-dd}T14:00", endsAt = $"{Tomorrow:yyyy-MM-dd}T16:00", reason = "Médico" });
        var blocked = await client.PostAsJsonAsync("/api/appointments", PanelBooking(s, Tomorrow, "15:00"));
        await ApiFactory.Expect(blocked, HttpStatusCode.Conflict);
        Assert.Contains("bloqueado", (await ApiFactory.Json(blocked)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Status_follows_the_rules_and_can_be_undone()
    {
        var s = await SetupAsync();
        var client = s.Business.Client;

        var future = (await CreateAsync(client, PanelBooking(s, Tomorrow, "09:00"))).GetProperty("id").GetGuid();
        await ApiFactory.Expect(await SetStatusAsync(client, future, "Completed"), HttpStatusCode.Conflict);

        // Una cita de ayer (alguien que llegó sin reservar) sí se marca como atendida o como inasistencia.
        var past = (await CreateAsync(client, PanelBooking(s, Yesterday, "09:00"))).GetProperty("id").GetGuid();
        var completed = await SetStatusAsync(client, past, "Completed");
        await ApiFactory.Expect(completed, HttpStatusCode.OK);
        Assert.Equal("Completed", (await ApiFactory.Json(completed)).GetProperty("status").GetString());
        await ApiFactory.Expect(await SetStatusAsync(client, past, "NoShow"), HttpStatusCode.OK);
        await ApiFactory.Expect(await SetStatusAsync(client, past, "Confirmed"), HttpStatusCode.OK);

        // Cancelar libera el horario; reactivar solo si sigue libre.
        await ApiFactory.Expect(await SetStatusAsync(client, future, "Cancelled"), HttpStatusCode.OK);
        var taken = await CreateAsync(client, PanelBooking(s, Tomorrow, "09:00"));
        await ApiFactory.Expect(await SetStatusAsync(client, future, "Confirmed"), HttpStatusCode.Conflict);
        await ApiFactory.Expect(await SetStatusAsync(client, taken.GetProperty("id").GetGuid(), "Cancelled"), HttpStatusCode.OK);
        await ApiFactory.Expect(await SetStatusAsync(client, future, "Confirmed"), HttpStatusCode.OK);

        // Una cancelada que ya pasó no se reactiva.
        await ApiFactory.Expect(await SetStatusAsync(client, past, "Cancelled"), HttpStatusCode.OK);
        await ApiFactory.Expect(await SetStatusAsync(client, past, "Confirmed"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Appointments_move_to_free_times_and_people()
    {
        var s = await SetupAsync();
        var client = s.Business.Client;
        var a = (await CreateAsync(client, PanelBooking(s, Tomorrow, "09:00"))).GetProperty("id").GetGuid();
        await CreateAsync(client, PanelBooking(s, Tomorrow, "11:00"));

        var moved = await client.PutAsJsonAsync($"/api/appointments/{a}/move",
            new { staffMemberId = s.OtherStaffId, date = Tomorrow.ToString("yyyy-MM-dd"), time = "11:00" });
        await ApiFactory.Expect(moved, HttpStatusCode.OK);
        var body = await ApiFactory.Json(moved);
        Assert.Equal("Beto", body.GetProperty("staffName").GetString());
        Assert.Equal($"{Tomorrow:yyyy-MM-dd}T11:30:00", body.GetProperty("endsAt").GetString());

        // A las 11:00 Ana ya está ocupada.
        await ApiFactory.Expect(await client.PutAsJsonAsync($"/api/appointments/{a}/move",
            new { staffMemberId = s.StaffId, date = Tomorrow.ToString("yyyy-MM-dd"), time = "11:15" }), HttpStatusCode.Conflict);

        await SetStatusAsync(client, a, "Cancelled");
        await ApiFactory.Expect(await client.PutAsJsonAsync($"/api/appointments/{a}/move",
            new { staffMemberId = s.StaffId, date = Tomorrow.ToString("yyyy-MM-dd"), time = "14:00" }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Agenda_and_customers_are_isolated_between_businesses()
    {
        var a = await SetupAsync();
        var b = await SetupAsync();
        var appointment = await CreateAsync(a.Business.Client, PanelBooking(a, Tomorrow, "09:00"));
        var appointmentId = appointment.GetProperty("id").GetGuid();
        var customerId = appointment.GetProperty("customerId").GetGuid();

        var agendaOfB = await ApiFactory.Json(await b.Business.Client.GetAsync($"/api/appointments/agenda?from={Tomorrow:yyyy-MM-dd}&to={Tomorrow:yyyy-MM-dd}"));
        Assert.Equal(0, agendaOfB.GetProperty("appointments").GetArrayLength());

        await ApiFactory.Expect(await SetStatusAsync(b.Business.Client, appointmentId, "Cancelled"), HttpStatusCode.NotFound);
        await ApiFactory.Expect(await b.Business.Client.PutAsJsonAsync($"/api/appointments/{appointmentId}/move",
            new { staffMemberId = b.StaffId, date = Tomorrow.ToString("yyyy-MM-dd"), time = "10:00" }), HttpStatusCode.NotFound);
        await ApiFactory.Expect(await b.Business.Client.GetAsync($"/api/customers/{customerId}"), HttpStatusCode.NotFound);
        Assert.Equal(0, (await ApiFactory.Json(await b.Business.Client.GetAsync("/api/customers"))).GetArrayLength());

        // B no puede agendar con la gente ni con los clientes de A.
        var foreignStaff = await b.Business.Client.PostAsJsonAsync("/api/appointments", PanelBooking(b, Tomorrow, "09:00", staffId: a.StaffId));
        await ApiFactory.Expect(foreignStaff, HttpStatusCode.BadRequest);
        var foreignCustomer = await b.Business.Client.PostAsJsonAsync("/api/appointments", PanelBooking(b, Tomorrow, "09:00", customerId: customerId));
        await ApiFactory.Expect(foreignCustomer, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(foreignCustomer)).GetProperty("errors").TryGetProperty("customerId", out _));
    }

    [Fact]
    public async Task Customers_have_history_counts_search_and_notes()
    {
        var s = await SetupAsync();
        var client = s.Business.Client;
        var phone = NewPhone();

        var visit = (await CreateAsync(client, PanelBooking(s, Yesterday, "09:00", phone: phone))).GetProperty("id").GetGuid();
        await SetStatusAsync(client, visit, "Completed");
        var missed = (await CreateAsync(client, PanelBooking(s, Yesterday, "11:00", phone: phone))).GetProperty("id").GetGuid();
        await SetStatusAsync(client, missed, "NoShow");
        await CreateAsync(client, PanelBooking(s, Tomorrow, "09:00", phone: phone));

        var byName = await ApiFactory.Json(await client.GetAsync("/api/customers?search=carlos"));
        var row = Assert.Single(byName.EnumerateArray());
        Assert.Equal(1, row.GetProperty("visits").GetInt32());
        Assert.Equal(1, row.GetProperty("noShows").GetInt32());
        Assert.Equal($"{Tomorrow:yyyy-MM-dd}T09:00:00", row.GetProperty("nextAppointmentAt").GetString());

        var byPhone = await ApiFactory.Json(await client.GetAsync($"/api/customers?search={phone[^4..]}"));
        Assert.Single(byPhone.EnumerateArray());
        Assert.Equal(0, (await ApiFactory.Json(await client.GetAsync("/api/customers?search=%25"))).GetArrayLength());

        var id = row.GetProperty("id").GetGuid();
        var updated = await client.PutAsJsonAsync($"/api/customers/{id}", new { name = "Carlos Pérez", email = "carlos@correo.co", notes = "Prefiere a Ana." });
        await ApiFactory.Expect(updated, HttpStatusCode.OK);
        var detail = await ApiFactory.Json(updated);
        Assert.Equal("Prefiere a Ana.", detail.GetProperty("notes").GetString());
        Assert.False(detail.GetProperty("consentedOnline").GetBoolean());
        var history = detail.GetProperty("appointments").EnumerateArray().Select(a => a.GetProperty("startsAt").GetString()).ToList();
        Assert.Equal(3, history.Count);
        Assert.Equal($"{Tomorrow:yyyy-MM-dd}T09:00:00", history[0]); // la más reciente primero
    }

    [Fact]
    public async Task Staff_accounts_can_run_the_agenda()
    {
        var s = await SetupAsync();
        var staff = await factory.CreateStaffAccountAsync(s.Business.TenantId);

        var created = await CreateAsync(staff, PanelBooking(s, Yesterday, "15:00"));
        await ApiFactory.Expect(await SetStatusAsync(staff, created.GetProperty("id").GetGuid(), "Completed"), HttpStatusCode.OK);
        await ApiFactory.Expect(await staff.GetAsync("/api/customers"), HttpStatusCode.OK);
    }
}
