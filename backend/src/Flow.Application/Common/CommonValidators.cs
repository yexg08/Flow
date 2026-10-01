using System.Text.RegularExpressions;
using FluentValidation;

namespace Flow.Application.Common;

public static partial class CommonValidators
{
    public static IRuleBuilderOptions<T, string> ValidSlug<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("El enlace es obligatorio.")
            .Must(Slugs.HasValidFormat)
            .WithMessage($"Usa entre {Slugs.MinLength} y {Slugs.MaxLength} letras minúsculas, números o guiones (sin tildes ni espacios).")
            .Must(slug => !Slugs.IsReserved(slug)).WithMessage("Ese enlace está reservado. Elige otro.");

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MinimumLength(10).WithMessage("La contraseña debe tener al menos 10 caracteres.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.")
            .Must(p => p is not null && p.Any(char.IsLetter) && p.Any(char.IsDigit))
            .WithMessage("La contraseña debe tener letras y números.");

    /// <summary>Indicativo + número, solo dígitos (p. ej. 573001234567).</summary>
    public static IRuleBuilderOptions<T, string?> OptionalWhatsApp<T>(this IRuleBuilder<T, string?> rule) =>
        rule
            .Must(n => string.IsNullOrWhiteSpace(n) || WhatsAppFormat().IsMatch(n))
            .WithMessage("Escribe el número con indicativo y solo dígitos, p. ej. 573001234567.");

    public static IRuleBuilderOptions<T, string> HexColor<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(c => c is not null && HexColorFormat().IsMatch(c)).WithMessage("El color debe tener el formato #rrggbb.");

    [GeneratedRegex("^[0-9]{10,15}$")]
    private static partial Regex WhatsAppFormat();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorFormat();
}
