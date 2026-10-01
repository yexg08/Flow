using Flow.Application.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Flow.Api.Controllers;

/// <summary>Página pública de cada negocio (flow.app/n/{slug}). Sin sesión.</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
[EnableRateLimiting(RateLimits.Public)]
public class PublicController(IPublicBusinessService publicBusiness) : ControllerBase
{
    [HttpGet("businesses/{slug}")]
    public Task<PublicBusinessDto> Business(string slug, CancellationToken ct) =>
        publicBusiness.GetBySlugAsync(slug.ToLowerInvariant(), ct);
}
