using Flow.Application.Agenda;
using Flow.Application.Booking;
using Flow.Application.Customers;
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

/// <summary>
/// Citas y agenda del negocio de la sesión. Política de respaldo: dueños y empleados (quien atiende o recibe en la
/// recepción también agenda y marca asistencia).
/// </summary>
[ApiController]
[Route("api/appointments")]
public class AppointmentsController(IAppointmentsService appointments, IAgendaService agenda) : ControllerBase
{
    [HttpGet("upcoming")]
    public Task<IReadOnlyList<UpcomingAppointmentDto>> Upcoming([FromQuery] int limit = 20, CancellationToken ct = default) =>
        appointments.ListUpcomingAsync(limit, ct);

    /// <summary>Citas y bloqueos entre dos fechas (incluidas), en hora local del negocio.</summary>
    [HttpGet("agenda")]
    public Task<AgendaDto> Agenda([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        agenda.GetAsync(from, to, ct);

    [HttpPost]
    public async Task<ActionResult<AgendaAppointmentDto>> Create(PanelBookingRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await agenda.CreateAsync(request, ct));

    [HttpPut("{id:guid}/move")]
    public Task<AgendaAppointmentDto> Move(Guid id, MoveAppointmentRequest request, CancellationToken ct) =>
        agenda.MoveAsync(id, request, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<AgendaAppointmentDto> SetStatus(Guid id, SetStatusRequest request, CancellationToken ct) =>
        agenda.SetStatusAsync(id, request, ct);
}

/// <summary>Clientes del negocio de la sesión.</summary>
[ApiController]
[Route("api/customers")]
public class CustomersController(ICustomersService customers) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CustomerSummaryDto>> List([FromQuery] string? search, CancellationToken ct) =>
        customers.ListAsync(search, ct);

    [HttpGet("{id:guid}")]
    public Task<CustomerDetailDto> Get(Guid id, CancellationToken ct) => customers.GetAsync(id, ct);

    [HttpPut("{id:guid}")]
    public Task<CustomerDetailDto> Update(Guid id, UpdateCustomerRequest request, CancellationToken ct) =>
        customers.UpdateAsync(id, request, ct);
}
