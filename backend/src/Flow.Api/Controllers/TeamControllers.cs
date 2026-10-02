using Flow.Api.Auth;
using Flow.Application.Team;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers;

/// <summary>Equipo del negocio de la sesión. Todos los miembros lo ven; solo el dueño lo gestiona.</summary>
[ApiController]
[Route("api/staff")]
public class StaffController(ITeamService team, ITeamAccountsService accounts) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<StaffDto>> List(CancellationToken ct) => team.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<StaffDto> Get(Guid id, CancellationToken ct) => team.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<ActionResult<StaffDto>> Create(StaffUpsertRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await team.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.TenantOwner)]
    public Task<StaffDto> Update(Guid id, StaffUpsertRequest request, CancellationToken ct) => team.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        // Borra también su cuenta del panel, si tenía (todo o nada).
        await accounts.DeleteStaffAsync(id, ct);
        return NoContent();
    }

    /// <summary>Quién del equipo tiene acceso al panel.</summary>
    [HttpGet("accounts")]
    [Authorize(Policy = Policies.TenantOwner)]
    public Task<IReadOnlyList<StaffAccountDto>> Accounts(CancellationToken ct) => accounts.ListAsync(ct);

    /// <summary>Da acceso al panel. La contraseña temporal solo se ve en esta respuesta.</summary>
    [HttpPost("{id:guid}/account")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<ActionResult<TemporaryPasswordDto>> GrantAccess(Guid id, GrantAccessRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await accounts.GrantAsync(id, request, ct));

    [HttpPost("{id:guid}/account/reset-password")]
    [Authorize(Policy = Policies.TenantOwner)]
    public Task<TemporaryPasswordDto> ResetPassword(Guid id, CancellationToken ct) => accounts.ResetPasswordAsync(id, ct);

    [HttpDelete("{id:guid}/account")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<IActionResult> RevokeAccess(Guid id, CancellationToken ct)
    {
        await accounts.RevokeAsync(id, ct);
        return NoContent();
    }

    /// <summary>Reemplaza el horario semanal completo.</summary>
    [HttpPut("{id:guid}/schedule")]
    [Authorize(Policy = Policies.TenantOwner)]
    public Task<StaffDto> SetSchedule(Guid id, ScheduleRequest request, CancellationToken ct) => team.SetScheduleAsync(id, request, ct);
}

/// <summary>Bloqueos (vacaciones, festivos...) del negocio de la sesión.</summary>
[ApiController]
[Route("api/time-off")]
public class TimeOffController(ITimeOffService timeOff) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TimeOffDto>> List(CancellationToken ct) => timeOff.ListUpcomingAsync(ct);

    [HttpPost]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<ActionResult<TimeOffDto>> Create(TimeOffRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await timeOff.CreateAsync(request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await timeOff.DeleteAsync(id, ct);
        return NoContent();
    }
}
