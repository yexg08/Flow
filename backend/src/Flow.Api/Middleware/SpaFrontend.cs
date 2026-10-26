using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticFiles;

namespace Flow.Api.Middleware;

/// <summary>
/// frontend compilado (Angular) que la API sirve cuando existe wwwroot/index.html (como en la imagen de Docker)
/// en desarrollo no existe (Angular corre aparte con ng serve y la API solo devuelve JSON)
/// </summary>
public sealed partial class SpaFrontend
{
    private SpaFrontend(string contentSecurityPolicy) => ContentSecurityPolicy = contentSecurityPolicy;

    /// <summary>politica de contenido de las paginas del frontend (no de las respuestas JSON)</summary>
    public string ContentSecurityPolicy { get; }

    public static SpaFrontend? TryLoad(string? webRootPath)
    {
        var indexPath = webRootPath is null ? null : Path.Combine(webRootPath, "index.html");
        if (indexPath is null || !File.Exists(indexPath)) return null;

        // angular deja scripts en linea en index.html (aplicar el tema y activar la hoja de estilos)
        // en vez de permitir cualquier script en linea se autoriza el contenido exacto de cada uno por su hash
        var html = File.ReadAllText(indexPath);
        var hashes = InlineScript().Matches(html)
            .Select(m => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(m.Groups[1].Value)))}'");

        var policy = string.Join("; ",
            "default-src 'self'",
            $"script-src 'self' {string.Join(' ', hashes)}".TrimEnd(),
            // los componentes de angular agregan sus estilos como etiquetas style al cargarse
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
            "font-src https://fonts.gstatic.com",
            "img-src 'self' data:",
            "connect-src 'self'",
            "base-uri 'self'",
            "form-action 'self'",
            "object-src 'none'",
            "frame-ancestors 'none'");
        return new SpaFrontend(policy);
    }

    // los archivos con hash en el nombre nunca cambian (el navegador puede guardarlos mucho tiempo)
    public static void SetCacheHeaders(StaticFileResponseContext context) =>
        context.Context.Response.Headers.CacheControl = HashedAsset().IsMatch(context.File.Name)
            ? "public, max-age=31536000, immutable"
            : "no-cache";

    [GeneratedRegex(@"<script(?![^>]*\ssrc=)[^>]*>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex InlineScript();

    [GeneratedRegex(@"^(main|chunk|styles|polyfills)-[\w-]+\.(js|css)$")]
    private static partial Regex HashedAsset();
}
