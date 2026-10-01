using System.Net;
using System.Net.Http.Json;
using Flow.Application.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Flow.Tests.Integration;

[Collection(ApiCollection.Name)]
public class BookingTests(ApiFactory factory)
{
    private static readonly TimeZoneInfo Bogota = TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");

    /// <summary>Mañana en Bogotá: todos los horarios desde las 8:00 están en el futuro.</summary>
    private static DateOnly Tomorrow => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Bogota)).AddDays(1);

    private static string NewPhone() => "3" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    private sealed record Setup(ApiFactory.Business Business, Guid ServiceId, List<Guid> StaffIds);

    /// <summary>Negocio con un servicio de 60 minutos y personas que lo atienden todos los días de 08:00 a 12:00.</summary>
    private async Task<Setup> SetupAsync(int staffCount = 1, int durationMinutes = 60)
    {
        var business = await factory.RegisterBusinessAsync("Barbería de reservas");
        var serviceResponse = await business.Client.PostAsJsonAsync("/api/services", ApiFactory.NewService("Corte", durationMinutes, 30000));
        await ApiFactory.Expect(serviceResponse, HttpStatusCode.Created);
        var serviceId = (await ApiFactory.Json(serviceResponse)).GetProperty("id").GetGuid();

        var staffIds = new List<Guid>();
        for (var i = 0; i < staffCount; i++)
        {
            var staff = await business.Client.PostAsJsonAsync("/api/staff",
                new { name = $"Persona {i + 1}", color = "#22c55e", isActive = true, serviceIds = new[] { serviceId } });
            await ApiFactory.Expect(staff, HttpStatusCode.Created);
            var staffId = (await ApiFactory.Json(staff)).GetProperty("id").GetGuid();
            var everyDay = Enum.GetNames<DayOfWeek>().Select(d => new { dayOfWeek = d, start = "08:00", end = "12:00" });
            await ApiFactory.Expect(await business.Client.PutAsJsonAsync($"/api/staff/{staffId}/schedule", new { ranges = everyDay }), HttpStatusCode.OK);
            staffIds.Add(staffId);
        }

        return new Setup(business, serviceId, staffIds);
    }

    private static object Book(Guid serviceId, Guid? staffId, DateOnly date, string time, string? phone = null, bool acceptsPrivacy = true) => new
    {
        serviceId, staffMemberId = staffId, date = date.ToString("yyyy-MM-dd"), time,
        customerName = "Cliente de prueba", customerPhone = phone ?? NewPhone(), customerEmail = (string?)null,
        note = (string?)null, acceptsPrivacy
    };

    private Task<HttpResponseMessage> PostBookingAsync(string slug, object body) =>
        factory.NewClient().PostAsJsonAsync($"/api/public/businesses/{slug}/appointments", body);

    private async Task<List<string>> SlotsAsync(string slug, Guid serviceId, DateOnly date, Guid? staffId = null)
    {
        var url = $"/api/public/businesses/{slug}/availability?serviceId={serviceId}&date={date:yyyy-MM-dd}"
            + (staffId is null ? "" : $"&staffId={staffId}");
        var response = await factory.NewClient().GetAsync(url);
        await ApiFactory.Expect(response, HttpStatusCode.OK);
        return (await ApiFactory.Json(response)).GetProperty("slots").EnumerateArray().Select(s => s.GetProperty("time").GetString()!).ToList();
    }

    [Fact]
    public async Task Availability_offers_every_15_minutes_where_the_whole_service_fits()
    {
        var s = await SetupAsync();
        var slots = await SlotsAsync(s.Business.Slug, s.ServiceId, Tomorrow);

        // 60 minutos dentro de 08:00–12:00: desde 08:00 hasta 11:00 (la última que termina a las 12:00).
        Assert.Equal(13, slots.Count);
        Assert.Equal("08:00", slots.First());
        Assert.Equal("11:00", slots.Last());
    }

    [Fact]
    public async Task A_booking_takes_its_slot_and_the_overlapping_ones()
    {
        var s = await SetupAsync();
        var response = await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:00"));
        await ApiFactory.Expect(response, HttpStatusCode.Created);

        var body = await ApiFactory.Json(response);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("manageToken").GetString()));
        var appointment = body.GetProperty("appointment");
        Assert.Equal($"{Tomorrow:yyyy-MM-dd}T09:00:00", appointment.GetProperty("startsAt").GetString());
        Assert.Equal("Persona 1", appointment.GetProperty("staffName").GetString());
        Assert.Equal(30000m, appointment.GetProperty("price").GetDecimal());

        var slots = await SlotsAsync(s.Business.Slug, s.ServiceId, Tomorrow);
        Assert.Contains("08:00", slots);   // 08:00–09:00 solo toca la cita, no se cruza
        Assert.DoesNotContain("08:15", slots);
        Assert.DoesNotContain("09:00", slots);
        Assert.DoesNotContain("09:45", slots);
        Assert.Contains("10:00", slots);

        // Y el mismo horario ya no se puede reservar.
        await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, s.StaffIds[0], Tomorrow, "09:30")), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Simultaneous_bookings_for_the_same_slot_only_one_wins()
    {
        var s = await SetupAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => PostBookingAsync(s.Business.Slug, Book(s.ServiceId, s.StaffIds[0], Tomorrow, "10:00"))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var stored = await factory.WithDbAsync(s.Business.TenantId, (db, _) =>
            db.Appointments.CountAsync(a => a.StaffMemberId == s.StaffIds[0] && a.Status == AppointmentStatus.Confirmed));
        Assert.Equal(1, stored);
    }

    [Fact]
    public async Task The_database_rejects_overlapping_appointments_even_bypassing_the_app()
    {
        var s = await SetupAsync();
        var start = new DateTime(2099, 6, 1, 14, 0, 0, DateTimeKind.Utc);

        Task<int> InsertAsync(DateTime from, DateTime to, AppointmentStatus status) => factory.WithDbAsync(s.Business.TenantId, async (db, _) =>
        {
            var customer = new Customer { Name = "Directo", Phone = NewPhone(), PrivacyConsentAt = DateTime.UtcNow };
            db.Customers.Add(customer);
            db.Appointments.Add(new Appointment
            {
                CustomerId = customer.Id, ServiceId = s.ServiceId, StaffMemberId = s.StaffIds[0], StartsAtUtc = from, EndsAtUtc = to,
                Status = status, ManageTokenHash = SecureTokens.Hash(SecureTokens.Generate())
            });
            return await db.SaveChangesAsync();
        });

        await InsertAsync(start, start.AddHours(1), AppointmentStatus.Confirmed);
        await Assert.ThrowsAsync<ScheduleConflictException>(() => InsertAsync(start.AddMinutes(30), start.AddMinutes(90), AppointmentStatus.Confirmed));

        // Una cita cancelada no ocupa el horario, y una que empieza justo cuando termina la otra tampoco se cruza.
        await InsertAsync(start.AddMinutes(30), start.AddMinutes(90), AppointmentStatus.Cancelled);
        await InsertAsync(start.AddHours(1), start.AddHours(2), AppointmentStatus.Confirmed);
    }

    [Fact]
    public async Task Any_staff_spreads_bookings_between_free_people()
    {
        var s = await SetupAsync(staffCount: 2);

        var first = await ApiFactory.Json(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:00")));
        var second = await ApiFactory.Json(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:00")));
        Assert.NotEqual(
            first.GetProperty("appointment").GetProperty("staffMemberId").GetGuid(),
            second.GetProperty("appointment").GetProperty("staffMemberId").GetGuid());

        await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:00")), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Time_off_and_inactive_people_hide_their_slots()
    {
        var s = await SetupAsync();
        var block = await s.Business.Client.PostAsJsonAsync("/api/time-off", new
        {
            staffMemberId = (Guid?)null, startsAt = $"{Tomorrow:yyyy-MM-dd}T00:00", endsAt = $"{Tomorrow.AddDays(1):yyyy-MM-dd}T00:00", reason = "Festivo"
        });
        await ApiFactory.Expect(block, HttpStatusCode.Created);
        Assert.Empty(await SlotsAsync(s.Business.Slug, s.ServiceId, Tomorrow));

        await s.Business.Client.DeleteAsync($"/api/time-off/{(await ApiFactory.Json(block)).GetProperty("id").GetGuid()}");
        Assert.NotEmpty(await SlotsAsync(s.Business.Slug, s.ServiceId, Tomorrow));

        await s.Business.Client.PutAsJsonAsync($"/api/staff/{s.StaffIds[0]}",
            new { name = "Persona 1", color = "#22c55e", isActive = false, serviceIds = new[] { s.ServiceId } });
        Assert.Empty(await SlotsAsync(s.Business.Slug, s.ServiceId, Tomorrow));
    }

    [Fact]
    public async Task Customer_manages_the_appointment_with_the_private_link()
    {
        var s = await SetupAsync();
        var booked = await ApiFactory.Json(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:00")));
        var token = booked.GetProperty("manageToken").GetString()!;
        var client = factory.NewClient();

        var details = await ApiFactory.Json(await client.GetAsync($"/api/public/appointments/{token}"));
        Assert.True(details.GetProperty("canChange").GetBoolean());
        Assert.Equal("Barbería de reservas", details.GetProperty("businessName").GetString());

        // Reprogramar a las 11:00 libera las 9:00.
        var moved = await client.PostAsJsonAsync($"/api/public/appointments/{token}/reschedule", new { date = Tomorrow.ToString("yyyy-MM-dd"), time = "11:00" });
        await ApiFactory.Expect(moved, HttpStatusCode.OK);
        Assert.Equal($"{Tomorrow:yyyy-MM-dd}T11:00:00", (await ApiFactory.Json(moved)).GetProperty("startsAt").GetString());
        Assert.Contains("09:00", await SlotsAsync(s.Business.Slug, s.ServiceId, Tomorrow));

        // Cancelar libera el horario y ya no se puede volver a cambiar.
        var cancelled = await client.PostAsync($"/api/public/appointments/{token}/cancel", null);
        await ApiFactory.Expect(cancelled, HttpStatusCode.OK);
        Assert.Equal("Cancelled", (await ApiFactory.Json(cancelled)).GetProperty("status").GetString());
        Assert.Contains("11:00", await SlotsAsync(s.Business.Slug, s.ServiceId, Tomorrow));
        await ApiFactory.Expect(await client.PostAsync($"/api/public/appointments/{token}/cancel", null), HttpStatusCode.Conflict);

        await ApiFactory.Expect(await client.GetAsync($"/api/public/appointments/{SecureTokens.Generate()}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Bookings_cannot_use_another_business_data()
    {
        var a = await SetupAsync();
        var b = await SetupAsync();

        var foreignService = await PostBookingAsync(a.Business.Slug, Book(b.ServiceId, null, Tomorrow, "09:00"));
        await ApiFactory.Expect(foreignService, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(foreignService)).GetProperty("errors").TryGetProperty("serviceId", out _));

        var foreignStaff = await PostBookingAsync(a.Business.Slug, Book(a.ServiceId, b.StaffIds[0], Tomorrow, "09:00"));
        await ApiFactory.Expect(foreignStaff, HttpStatusCode.BadRequest);
        Assert.True((await ApiFactory.Json(foreignStaff)).GetProperty("errors").TryGetProperty("staffMemberId", out _));

        await ApiFactory.Expect(
            await factory.NewClient().GetAsync($"/api/public/businesses/{a.Business.Slug}/availability?serviceId={b.ServiceId}&date={Tomorrow:yyyy-MM-dd}"),
            HttpStatusCode.NotFound);

        // Lo reservado en A no aparece en el panel de B.
        await ApiFactory.Expect(await PostBookingAsync(a.Business.Slug, Book(a.ServiceId, null, Tomorrow, "09:00")), HttpStatusCode.Created);
        Assert.Equal(1, (await ApiFactory.Json(await a.Business.Client.GetAsync("/api/appointments/upcoming"))).GetArrayLength());
        Assert.Equal(0, (await ApiFactory.Json(await b.Business.Client.GetAsync("/api/appointments/upcoming"))).GetArrayLength());
    }

    [Fact]
    public async Task Invalid_bookings_are_rejected()
    {
        var s = await SetupAsync();

        async Task ExpectFieldError(object body, string field)
        {
            var response = await PostBookingAsync(s.Business.Slug, body);
            await ApiFactory.Expect(response, HttpStatusCode.BadRequest);
            Assert.True((await ApiFactory.Json(response)).GetProperty("errors").TryGetProperty(field, out _), $"Falta el error de {field}");
        }

        await ExpectFieldError(Book(s.ServiceId, null, Tomorrow, "09:00", acceptsPrivacy: false), "acceptsPrivacy");
        await ExpectFieldError(Book(s.ServiceId, null, Tomorrow, "09:00", phone: "llámame"), "customerPhone");
        await ExpectFieldError(Book(s.ServiceId, null, Tomorrow.AddDays(-2), "09:00"), "date");
        await ExpectFieldError(Book(s.ServiceId, null, Tomorrow.AddDays(90), "09:00"), "date");

        // Una hora fuera del horario o que no cae en la grilla de 15 minutos no está disponible.
        await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "15:00")), HttpStatusCode.Conflict);
        await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:05")), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_phone_can_hold_only_a_few_upcoming_appointments()
    {
        var s = await SetupAsync(durationMinutes: 30);
        var phone = NewPhone();

        foreach (var time in new[] { "08:00", "08:30", "09:00" })
            await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, time, phone)), HttpStatusCode.Created);

        await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "10:00", phone)), HttpStatusCode.Conflict);

        // Es el mismo cliente, no tres distintos.
        var customers = await factory.WithDbAsync(s.Business.TenantId, (db, _) => db.Customers.CountAsync(c => c.Phone == phone));
        Assert.Equal(1, customers);
    }

    [Fact]
    public async Task Services_and_people_with_appointments_are_deactivated_not_deleted()
    {
        var s = await SetupAsync();
        await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:00")), HttpStatusCode.Created);

        await ApiFactory.Expect(await s.Business.Client.DeleteAsync($"/api/services/{s.ServiceId}"), HttpStatusCode.Conflict);
        await ApiFactory.Expect(await s.Business.Client.DeleteAsync($"/api/staff/{s.StaffIds[0]}"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Public_page_lists_only_people_who_can_take_bookings()
    {
        var s = await SetupAsync();
        await s.Business.Client.PostAsJsonAsync("/api/staff", new { name = "Sin horario", color = "#22c55e", isActive = true, serviceIds = new[] { s.ServiceId } });

        var page = await ApiFactory.Json(await factory.NewClient().GetAsync($"/api/public/businesses/{s.Business.Slug}"));
        var person = Assert.Single(page.GetProperty("staff").EnumerateArray());
        Assert.Equal("Persona 1", person.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Suspended_business_links_stop_working()
    {
        var s = await SetupAsync();
        var token = (await ApiFactory.Json(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "09:00"))))
            .GetProperty("manageToken").GetString();

        var admin = await factory.LoginAsSuperAdminAsync();
        await admin.PatchAsJsonAsync($"/api/admin/tenants/{s.Business.TenantId}/status", new { isActive = false });

        await ApiFactory.Expect(await factory.NewClient().GetAsync($"/api/public/appointments/{token}"), HttpStatusCode.NotFound);
        await ApiFactory.Expect(await PostBookingAsync(s.Business.Slug, Book(s.ServiceId, null, Tomorrow, "10:00")), HttpStatusCode.NotFound);
    }
}
