using Flow.Api.Auth;
using Flow.Application.Business;
using Flow.Application.Catalog;
using Flow.Application.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Controllers;

/// <summary>Ajustes del negocio de la sesión. Sin atributo de clase: aplica la política de respaldo (miembro del negocio).</summary>
[ApiController]
[Route("api/business")]
public class BusinessController(IBusinessService business, IMetricsService metrics) : ControllerBase
{
    [HttpGet]
    public Task<BusinessDto> Get(CancellationToken ct) => business.GetCurrentAsync(ct);

    /// <summary>Métricas de los últimos días (7 a 365). Solo el dueño: incluyen ingresos.</summary>
    [HttpGet("metrics")]
    [Authorize(Policy = Policies.TenantOwner)]
    public Task<MetricsDto> Metrics([FromQuery] int days = 30, CancellationToken ct = default) => metrics.GetAsync(days, ct);

    [HttpPut]
    [Authorize(Policy = Policies.TenantOwner)]
    public Task<BusinessDto> Update(UpdateBusinessRequest request, CancellationToken ct) => business.UpdateCurrentAsync(request, ct);
}

/// <summary>Servicios del negocio de la sesión. Los empleados los ven; solo el dueño los gestiona.</summary>
[ApiController]
[Route("api/services")]
public class ServicesController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ServiceDto>> List(CancellationToken ct) => catalog.ListAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<ServiceDto> Get(Guid id, CancellationToken ct) => catalog.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<ActionResult<ServiceDto>> Create(ServiceUpsertRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await catalog.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.TenantOwner)]
    public Task<ServiceDto> Update(Guid id, ServiceUpsertRequest request, CancellationToken ct) => catalog.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.TenantOwner)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await catalog.DeleteAsync(id, ct);
        return NoContent();
    }
}
