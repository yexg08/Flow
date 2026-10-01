using System.Threading.RateLimiting;

namespace Flow.Api;

/// <summary>Límites por IP de los endpoints sin sesión (configurables para las pruebas).</summary>
public static class RateLimits
{
    public const string Auth = "auth";
    public const string SlugCheck = "slug-check";
    public const string Public = "public";

    public static IServiceCollection AddFlowRateLimits(this IServiceCollection services, IConfiguration config)
    {
        var auth = config.GetValue("RateLimiting:AuthPerMinute", 10);
        var slug = config.GetValue("RateLimiting:SlugCheckPerMinute", 60);
        var publicPages = config.GetValue("RateLimiting:PublicPerMinute", 120);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Auth, context => PerIp(context, auth));
            options.AddPolicy(SlugCheck, context => PerIp(context, slug));
            options.AddPolicy(Public, context => PerIp(context, publicPages));
        });
    }

    private static RateLimitPartition<string> PerIp(HttpContext context, int perMinute) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) });
}
