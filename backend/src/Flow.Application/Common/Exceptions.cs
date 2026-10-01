namespace Flow.Application.Common;

/// <summary>El recurso no existe o es de otro negocio (no se distingue a propósito). HTTP 404.</summary>
public class NotFoundException(string resource) : Exception($"{resource} no existe.");

/// <summary>Se viola una regla de negocio. HTTP 409.</summary>
public class BusinessRuleException(string message) : Exception(message);

/// <summary>
/// El horario se cruza con otra cita de la misma persona. Lo lanza la validación de la app y también la traducción
/// del error de la restricción de exclusión de PostgreSQL (cuando dos reservas llegan al mismo tiempo). HTTP 409.
/// </summary>
public class ScheduleConflictException() : BusinessRuleException("Ese horario ya no está disponible. Elige otro.");

/// <summary>Dato inválido que solo se puede comprobar contra la base de datos. HTTP 400 asociado al campo.</summary>
public class FieldValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}

/// <summary>Credenciales o sesión inválidas. HTTP 401.</summary>
public class AuthenticationFailedException(string message) : Exception(message);
