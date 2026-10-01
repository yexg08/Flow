using System.Security.Cryptography;
using System.Text;

namespace Flow.Application.Common;

/// <summary>Conversión entre la hora local del negocio (su zona horaria) y UTC, que es como se guarda todo.</summary>
public static class BusinessTime
{
    public static TimeZoneInfo Zone(string timeZoneId) => TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

    /// <summary>Falso si esa hora local no existe (el salto del cambio de horario).</summary>
    public static bool TryToUtc(DateTime local, TimeZoneInfo zone, out DateTime utc)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            utc = default;
            return false;
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        return true;
    }

    public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone) =>
        DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone), DateTimeKind.Unspecified);

    /// <summary>Inicio del día local en UTC. Si la medianoche no existe en esa zona, el primer instante válido.</summary>
    public static DateTime StartOfDayUtc(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(15);
        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    public static DateOnly Today(TimeZoneInfo zone, TimeProvider timeProvider) =>
        DateOnly.FromDateTime(ToLocal(timeProvider.GetUtcNow().UtcDateTime, zone));
}

/// <summary>Tokens aleatorios para enlaces privados (p. ej. gestionar una cita). En la base solo se guarda el hash.</summary>
public static class SecureTokens
{
    /// <summary>256 bits aleatorios en base64 apto para URL.</summary>
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public static class Phones
{
    /// <summary>"+57 300 123 4567" → "573001234567". Null si no parece un número de teléfono.</summary>
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        var allowed = phone.All(c => char.IsAsciiDigit(c) || c is ' ' or '-' or '(' or ')' or '+' or '.');
        return allowed && digits.Length is >= 7 and <= 15 ? digits : null;
    }
}
