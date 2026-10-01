using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Flow.Application.Common;

/// <summary>Reglas del identificador público de un negocio (flow.app/n/{slug}).</summary>
public static partial class Slugs
{
    public const int MinLength = 3;
    public const int MaxLength = 40;

    private static readonly HashSet<string> Reserved =
    [
        "admin", "superadmin", "api", "app", "auth", "login", "registro", "panel", "n", "www", "flow",
        "soporte", "ayuda", "assets", "static", "public", "dashboard", "cuenta", "precios", "demo"
    ];

    /// <summary>Convierte un nombre en slug: "Peluquería Blue" → "peluqueria-blue".</summary>
    public static string FromName(string name)
    {
        var decomposed = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-');
        }

        var slug = Dashes().Replace(builder.ToString(), "-").Trim('-');
        return slug.Length > MaxLength ? slug[..MaxLength].TrimEnd('-') : slug;
    }

    public static bool HasValidFormat(string? slug) => slug is not null && Format().IsMatch(slug);

    public static bool IsReserved(string slug) => Reserved.Contains(slug);

    // Minúsculas, números y guiones; empieza y termina en letra o número; sin guiones dobles.
    [GeneratedRegex("^(?=.{3,40}$)[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Format();

    [GeneratedRegex("-{2,}")]
    private static partial Regex Dashes();
}
