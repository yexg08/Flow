using System.Globalization;

namespace Flow.Application.Team;

/// <summary>Un tramo del horario semanal, con horas locales del negocio en formato "HH:mm".</summary>
public record TimeRangeDto(DayOfWeek DayOfWeek, string Start, string End);

/// <summary>Reglas del horario semanal. Las usa el validador de la API; el frontend aplica las mismas para guiar.</summary>
public static class Schedule
{
    public const int MaxRangesPerDay = 6;
    public const int StepMinutes = 5;

    public static bool TryParseTime(string? value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Devuelve el primer problema del horario, o null si es válido.</summary>
    public static string? Validate(IReadOnlyList<TimeRangeDto>? ranges)
    {
        if (ranges is null) return "Envía el horario.";

        var parsed = new List<(DayOfWeek Day, TimeOnly Start, TimeOnly End)>();
        foreach (var range in ranges)
        {
            if (range is null) return "Hay un tramo vacío.";
            if (!Enum.IsDefined(range.DayOfWeek)) return "Hay un día que no es válido.";
            if (!TryParseTime(range.Start, out var start) || !TryParseTime(range.End, out var end))
                return "Las horas deben tener el formato HH:mm (p. ej. 08:30).";
            if (start.Minute % StepMinutes != 0 || end.Minute % StepMinutes != 0)
                return $"Las horas deben ir de {StepMinutes} en {StepMinutes} minutos.";
            if (end <= start) return $"En {DayName(range.DayOfWeek)}, la hora de cierre debe ser después de la de apertura.";
            parsed.Add((range.DayOfWeek, start, end));
        }

        foreach (var day in parsed.GroupBy(r => r.Day))
        {
            if (day.Count() > MaxRangesPerDay) return $"En {DayName(day.Key)} hay más de {MaxRangesPerDay} tramos.";
            var ordered = day.OrderBy(r => r.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Start < ordered[i - 1].End)
                    return $"En {DayName(day.Key)} hay tramos que se cruzan.";
            }
        }

        return null;
    }

    public static string DayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "lunes",
        DayOfWeek.Tuesday => "martes",
        DayOfWeek.Wednesday => "miércoles",
        DayOfWeek.Thursday => "jueves",
        DayOfWeek.Friday => "viernes",
        DayOfWeek.Saturday => "sábado",
        _ => "domingo"
    };
}
