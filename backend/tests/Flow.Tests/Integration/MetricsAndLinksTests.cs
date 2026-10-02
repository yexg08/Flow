using System.Net;
using System.Net.Http.Json;

namespace Flow.Tests.Integration;

[Collection(ApiCollection.Name)]
public class MetricsAndLinksTests(ApiFactory factory)
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Bogota));

    private sealed record Setup(ApiFactory.Business Business, Guid ServiceId, Guid StaffId);

    private async Task<Setup> SetupAsync()
    {
        var business = await factory.RegisterBusinessAsync();
        var serviceId = await ApiFactory.CreateServiceAsync(business.Client); // 30 min, $25.000
        var staff = await business.Client.PostAsJsonAsync("/api/staff",
            new { name = "Ana", color = "#ec4899", isActive = true, serviceIds = new[] { serviceId } });
        var staffId = (await ApiFactory.Json(staff)).GetProperty("id").GetGuid();
        var week = Enum.GetNames<DayOfWeek>().Select(d => new { dayOfWeek = d, start = "08:00", end = "18:00" });
        await business.Client.PutAsJsonAsync($"/api/staff/{staffId}/schedule", new { ranges = week });
        return new Setup(business, serviceId, staffId);
    }

    private static async Task<Guid> PanelAsync(Setup s, DateOnly date, string time)
    {
        var response = await s.Business.Client.PostAsJsonAsync("/api/appointments", new
        {
            serviceId = s.ServiceId, staffMemberId = s.StaffId, date = date.ToString("yyyy-MM-dd"), time,
            customerId = (Guid?)null, customerName = "Cliente", customerPhone = "3" + Random.Shared.NextInt64(100_000_000, 999_999_999),
            note = (string?)null
        });
        await ApiFactory.Expect(response, HttpStatusCode.Created);
        return (await ApiFactory.Json(response)).GetProperty("id").GetGuid();
    }

    private static Task SetStatusAsync(Setup s, Guid id, string status) =>
        s.Business.Client.PatchAsJsonAsync($"/api/appointments/{id}/status", new { status });

    [Fact]
    public async Task Metrics_summarize_the_period_and_what_is_coming()
    {
        var s = await SetupAsync();
        var yesterday = Today.AddDays(-1);
        await SetStatusAsync(s, await PanelAsync(s, yesterday, "09:00"), "Completed");
        await SetStatusAsync(s, await PanelAsync(s, yesterday, "11:00"), "NoShow");
        await SetStatusAsync(s, await PanelAsync(s, yesterday, "13:00"), "Cancelled");
        await PanelAsync(s, Today.AddDays(1), "09:00");
        var online = await factory.NewClient().PostAsJsonAsync($"/api/public/businesses/{s.Business.Slug}/appointments", new
        {
            serviceId = s.ServiceId, staffMemberId = (Guid?)null, date = Today.AddDays(1).ToString("yyyy-MM-dd"), time = "10:00",
            customerName = "En línea", customerPhone = "3009998877", customerEmail = (string?)null, note = (string?)null, acceptsPrivacy = true
        });
        await ApiFactory.Expect(online, HttpStatusCode.Created);

        var m = await ApiFactory.Json(await s.Business.Client.GetAsync("/api/business/metrics?days=7"));
        Assert.Equal(2, m.GetProperty("appointments").GetInt32());   // la cancelada no cuenta
        Assert.Equal(1, m.GetProperty("completed").GetInt32());
        Assert.Equal(1, m.GetProperty("noShows").GetInt32());
        Assert.Equal(1, m.GetProperty("cancelled").GetInt32());
        Assert.Equal(25000m, m.GetProperty("revenue").GetDecimal());
        Assert.Equal(0.5, m.GetProperty("noShowRate").GetDouble());
        Assert.Equal(0, m.GetProperty("onlineShare").GetDouble());  // las del periodo entraron por el panel
        Assert.Equal(2, m.GetProperty("upcomingAppointments").GetInt32());
        Assert.Equal(50000m, m.GetProperty("upcomingRevenue").GetDecimal());
        Assert.Equal(7, m.GetProperty("daily").GetArrayLength());
        var yesterdayPoint = m.GetProperty("daily").EnumerateArray().Single(d => d.GetProperty("date").GetString() == yesterday.ToString("yyyy-MM-dd"));
        Assert.Equal(2, yesterdayPoint.GetProperty("appointments").GetInt32());
        Assert.Equal("Ana", Assert.Single(m.GetProperty("byStaff").EnumerateArray()).GetProperty("name").GetString());

        // Solo el dueño las ve, y cada negocio ve las suyas.
        var staff = await factory.CreateStaffAccountAsync(s.Business.TenantId);
        await ApiFactory.Expect(await staff.GetAsync("/api/business/metrics"), HttpStatusCode.Forbidden);
        var other = await factory.RegisterBusinessAsync();
        var empty = await ApiFactory.Json(await other.Client.GetAsync("/api/business/metrics?days=7"));
        Assert.Equal(0, empty.GetProperty("appointments").GetInt32());
        Assert.Equal(0, empty.GetProperty("upcomingAppointments").GetInt32());
    }

    [Fact]
    public async Task The_business_can_resend_the_private_link_of_any_appointment()
    {
        var s = await SetupAsync();
        var id = await PanelAsync(s, Today.AddDays(2), "09:00");

        var link = await s.Business.Client.GetAsync($"/api/appointments/{id}/manage-link");
        await ApiFactory.Expect(link, HttpStatusCode.OK);
        var token = (await ApiFactory.Json(link)).GetProperty("token").GetString()!;
        Assert.Equal(token, (await ApiFactory.Json(await s.Business.Client.GetAsync($"/api/appointments/{id}/manage-link"))).GetProperty("token").GetString());

        var page = await ApiFactory.Json(await factory.NewClient().GetAsync($"/api/public/appointments/{token}"));
        Assert.Equal("Cliente", page.GetProperty("customerName").GetString());

        // El enlace que recibe quien reserva en línea es el mismo que el negocio puede reenviar.
        var booked = await ApiFactory.Json(await factory.NewClient().PostAsJsonAsync($"/api/public/businesses/{s.Business.Slug}/appointments", new
        {
            serviceId = s.ServiceId, staffMemberId = (Guid?)null, date = Today.AddDays(2).ToString("yyyy-MM-dd"), time = "11:00",
            customerName = "En línea", customerPhone = "3007776655", customerEmail = (string?)null, note = (string?)null, acceptsPrivacy = true
        }));
        var bookedId = booked.GetProperty("appointment").GetProperty("staffMemberId").GetGuid(); // para asegurarnos de que se creó
        Assert.Equal(s.StaffId, bookedId);
        var agenda = await ApiFactory.Json(await s.Business.Client.GetAsync(
            $"/api/appointments/agenda?from={Today.AddDays(2):yyyy-MM-dd}&to={Today.AddDays(2):yyyy-MM-dd}"));
        var onlineId = agenda.GetProperty("appointments").EnumerateArray().Single(a => a.GetProperty("customerName").GetString() == "En línea").GetProperty("id").GetGuid();
        var resent = (await ApiFactory.Json(await s.Business.Client.GetAsync($"/api/appointments/{onlineId}/manage-link"))).GetProperty("token").GetString();
        Assert.Equal(booked.GetProperty("manageToken").GetString(), resent);

        var other = await factory.RegisterBusinessAsync();
        await ApiFactory.Expect(await other.Client.GetAsync($"/api/appointments/{id}/manage-link"), HttpStatusCode.NotFound);
    }
}
