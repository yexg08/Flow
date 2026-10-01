namespace Flow.Api.Middleware;

/// <summary>Cabeceras de seguridad en todas las respuestas de la API (que solo devuelve JSON).</summary>
public static class SecurityHeaders
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, bool isDevelopment) =>
        app.Use((context, next) =>
        {
            // OnStarting y no antes de next(): el manejador de excepciones limpia las cabeceras al generar el error.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers.XContentTypeOptions = "nosniff";
                headers.XFrameOptions = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                // Swagger UI (solo en desarrollo) necesita cargar scripts y estilos.
                if (!isDevelopment) headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
                return Task.CompletedTask;
            });
            return next(context);
        });
}
