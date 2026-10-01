using Flow.Application.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Flow.Api.Controllers;

/// <summary>Reservas desde la página pública de cada negocio. Sin sesión.</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting(RateLimits.Public)]
public class PublicBookingController(IPublicBookingService booking) : ControllerBase
{
    [HttpGet("businesses/{slug}/availability")]
    public Task<PublicAvailabilityDto> Availability(
        string slug, [FromQuery] Guid serviceId, [FromQuery] DateOnly date, [FromQuery] Guid? staffId, CancellationToken ct) =>
        booking.GetAvailabilityAsync(slug, serviceId, date, staffId, ct);

    [HttpPost("businesses/{slug}/appointments")]
    [EnableRateLimiting(RateLimits.Booking)]
    public async Task<ActionResult<BookingResultDto>> Book(string slug, BookingRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await booking.BookAsync(slug, request, ct));

    /// <summary>El token del enlace que recibió el cliente al reservar es lo único que da acceso a su cita.</summary>
    [HttpGet("appointments/{token}")]
    public Task<PublicAppointmentDto> Appointment(string token, CancellationToken ct) => booking.GetByTokenAsync(token, ct);

    [HttpGet("appointments/{token}/availability")]
    public Task<PublicAvailabilityDto> RescheduleAvailability(string token, [FromQuery] DateOnly date, CancellationToken ct) =>
        booking.GetRescheduleAvailabilityAsync(token, date, ct);

    [HttpPost("appointments/{token}/reschedule")]
    [EnableRateLimiting(RateLimits.Booking)]
    public Task<PublicAppointmentDto> Reschedule(string token, RescheduleRequest request, CancellationToken ct) =>
        booking.RescheduleAsync(token, request, ct);

    [HttpPost("appointments/{token}/cancel")]
    [EnableRateLimiting(RateLimits.Booking)]
    public Task<PublicAppointmentDto> Cancel(string token, CancellationToken ct) => booking.CancelAsync(token, ct);
}

/// <summary>Citas del negocio de la sesión (política de respaldo: dueños y empleados).</summary>
[ApiController]
[Route("api/appointments")]
public class AppointmentsController(IAppointmentsService appointments) : ControllerBase
{
    [HttpGet("upcoming")]
    public Task<IReadOnlyList<UpcomingAppointmentDto>> Upcoming([FromQuery] int limit = 20, CancellationToken ct = default) =>
        appointments.ListUpcomingAsync(limit, ct);
}
