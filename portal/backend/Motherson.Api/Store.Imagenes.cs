using System.Security.Cryptography;
using System.Text;
using Dapper;

namespace Motherson;

/// Fotos de la pieza por material (17/11/2026).
/// Se guardan como archivos privados del portal en .local/imagenes-materiales, fuera de wwwroot,
/// y se sirven solo a sesiones autorizadas. No cambian las 8 tablas de la base compartida.
public sealed partial class Store
{
    public const long ImagenMaxBytes = 5 * 1024 * 1024;
    static readonly string[] ExtensionesImagen = [".png", ".jpg"];
    static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    string CarpetaImagenes => configuration["MVP_IMAGES_PATH"] ?? Path.Combine(Raiz, ".local", "imagenes-materiales");

    /// El número de parte puede tener espacios y sufijos: el nombre del archivo es su SHA-256.
    static string NombreImagen(string parte) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(parte))).ToLowerInvariant();

    string? RutaImagen(string parte) => ExtensionesImagen
        .Select(e => Path.Combine(CarpetaImagenes, NombreImagen(parte) + e)).FirstOrDefault(File.Exists);

    /// Solo se aceptan PNG y JPG reales: se revisa la firma del archivo, no solo su extensión.
    public static string? TipoImagen(ReadOnlySpan<byte> datos) =>
        datos.Length >= 8 && datos[..8].SequenceEqual(FirmaPng) ? ".png"
        : datos.Length >= 3 && datos[0] == 0xFF && datos[1] == 0xD8 && datos[2] == 0xFF ? ".jpg"
        : null;

    /// Versión por material para refrescar la imagen en el navegador cuando se reemplaza.
    internal Dictionary<string, long> VersionesImagenes(IEnumerable<string> partes)
    {
        var versiones = new Dictionary<string, long>(StringComparer.Ordinal);
        if (configuration["MVP_ROOT"] is null) return versiones;
        foreach (var parte in partes)
            if (RutaImagen(parte) is { } ruta) versiones[parte] = File.GetLastWriteTimeUtc(ruta).Ticks;
        return versiones;
    }

    async Task<string> MaterialAutorizado(Cuenta actor, string? parte)
    {
        if (string.IsNullOrEmpty(parte) || parte.Length > 100) throw new RuleException("Número de parte inválido.", 400);
        await using var c = await Open();
        var existe = await c.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM dbo.MATERIAL WHERE numero_parte=@parte AND (@puesto IS NULL OR id_puesto=@puesto)",
            new { parte, puesto = actor.PuestoId });
        return existe == 1 ? parte : throw new RuleException("Material no disponible.", 404);
    }

    public async Task<IResult> Imagen(Cuenta actor, string? parte)
    {
        var material = await MaterialAutorizado(actor, parte);
        var ruta = RutaImagen(material) ?? throw new RuleException("El material no tiene imagen.", 404);
        return Results.File(ruta, ruta.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg");
    }

    public async Task<object> GuardarImagen(Cuenta actor, string? parte, HttpRequest solicitud, CancellationToken token)
    {
        var material = await MaterialAutorizado(actor, parte);
        if (solicitud.ContentLength is > ImagenMaxBytes) throw new RuleException("La imagen pesa más de 5 MB.", 400);
        using var memoria = new MemoryStream();
        var bufer = new byte[81920];
        int leidos;
        while ((leidos = await solicitud.Body.ReadAsync(bufer, token)) > 0)
        {
            if (memoria.Length + leidos > ImagenMaxBytes) throw new RuleException("La imagen pesa más de 5 MB.", 400);
            memoria.Write(bufer, 0, leidos);
        }
        var datos = memoria.ToArray();
        var extension = TipoImagen(datos) ?? throw new RuleException("Solo se aceptan imágenes PNG o JPG.", 400);

        Directory.CreateDirectory(CarpetaImagenes);
        var destino = Path.Combine(CarpetaImagenes, NombreImagen(material) + extension);
        var temporal = destino + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllBytesAsync(temporal, datos, token);
        File.Move(temporal, destino, overwrite: true);
        // Si antes era JPG y ahora PNG (o al revés), se quita la anterior.
        foreach (var otra in ExtensionesImagen.Where(e => e != extension))
            File.Delete(Path.Combine(CarpetaImagenes, NombreImagen(material) + otra));
        return new { numero_parte = material, version = File.GetLastWriteTimeUtc(destino).Ticks };
    }

    public async Task<object> EliminarImagen(Cuenta actor, string? parte)
    {
        var material = await MaterialAutorizado(actor, parte);
        foreach (var e in ExtensionesImagen) File.Delete(Path.Combine(CarpetaImagenes, NombreImagen(material) + e));
        return new { numero_parte = material, eliminada = true };
    }
}
