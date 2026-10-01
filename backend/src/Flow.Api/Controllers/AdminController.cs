using Flow.Api.Auth;
using Flow.Application.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.SuperAdmin)]
public class AdminController(IAdminService admin) : ControllerBase
{
    [HttpGet("tenants")]
    public Task<IReadOnlyList<AdminTenantDto>> Tenants([FromQuery] string? search, CancellationToken ct) =>
        admin.ListTenantsAsync(search, ct);

    [HttpGet("stats")]
    public Task<AdminStatsDto> Stats(CancellationToken ct) => admin.GetStatsAsync(ct);

    [HttpPatch("tenants/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetTenantStatusRequest request, CancellationToken ct)
    {
        await admin.SetTenantStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
