using System.Security.Cryptography;
using System.Text;
using Flow.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Auth;

public class LinkOptions
{
    public const string SectionName = "Links";

    /// <summary>Clave HMAC de los enlaces de cita. Mínimo 32 caracteres, en user-secrets. Cambiarla invalida todos los enlaces.</summary>
    public string Key { get; set; } = string.Empty;
}

/// <summary>Token = HMAC-SHA256(clave, id de la cita) en base64 apto para URL (256 bits).</summary>
public class ManageLinks(IOptions<LinkOptions> options) : IManageLinks
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.Key);

    public string TokenFor(Guid appointmentId)
    {
        var mac = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes($"appointment:{appointmentId:N}"));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
