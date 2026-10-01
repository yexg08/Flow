using Flow.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Flow.Api.Middleware;

/// <summary>Traduce las excepciones a respuestas ProblemDetails con mensajes en español.</summary>
public class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is FieldValidationException fieldError)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var field = char.ToLowerInvariant(fieldError.Field[0]) + fieldError.Field[1..];
            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [fieldError.Message] })
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Hay errores en los datos enviados"
                }
            });
        }

        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "No encontrado", exception.Message),
            BusinessRuleException => (StatusCodes.Status409Conflict, "Operación no permitida", exception.Message),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "No autorizado", exception.Message),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Sin permiso", "No tienes permiso para hacer esto."),
            _ => (StatusCodes.Status500InternalServerError, "Error interno", "Ocurrió un error inesperado. Intenta de nuevo más tarde.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Error no controlado en {Method} {Path}", context.Request.Method, context.Request.Path);

        context.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
