namespace Flow.Api.Middleware;

/// <summary>cabeceras de seguridad en todas las respuestas (JSON de la API y paginas del frontend si lo sirve)</summary>
public static class SecurityHeaders
{
    private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app, bool isDevelopment, string? frontendPolicy = null) =>
        app.Use((context, next) =>
        {
            // se usa OnStarting y no antes de next() (el manejador de excepciones limpia las cabeceras al generar el error)
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

                // las rutas /api devuelven JSON y no necesitan cargar nada
                // las demas (si la API sirve el frontend) llevan la politica del frontend
                // swagger UI (solo en desarrollo) necesita cargar scripts y estilos
                var isApi = context.Request.Path.StartsWithSegments("/api");
                if (isApi && !isDevelopment) headers.ContentSecurityPolicy = ApiPolicy;
                else if (!isApi && frontendPolicy is not null) headers.ContentSecurityPolicy = frontendPolicy;
                else if (!isDevelopment) headers.ContentSecurityPolicy = ApiPolicy;
                return Task.CompletedTask;
            });
            return next(context);
        });
}
