using Microsoft.AspNetCore.Http;

namespace VetWeb.Services;

public class ImagenPerroService
{
    public const int MaximoBytes = 2 * 1024 * 1024;
    private readonly string _directorio;
    private readonly ILogger<ImagenPerroService> _logger;
    public ImagenPerroService(IWebHostEnvironment ambiente, IConfiguration configuracion, ILogger<ImagenPerroService> logger)
    {
        _directorio = Path.GetFullPath(configuracion["FotosPerros:Directorio"]
            ?? Path.Combine(ambiente.ContentRootPath, "App_Data", "Perros"));
        _logger = logger;
    }

    public record FotoValidada(byte[] Contenido, string Extension);

    public async Task<FotoValidada> ValidarAsync(IFormFile archivo)
    {
        if (archivo.Length == 0 || archivo.Length > MaximoBytes)
            throw new InvalidOperationException("La foto debe tener contenido y no superar los 2 MB.");
        await using var origen = archivo.OpenReadStream();
        using var buffer = new MemoryStream();
        var bloque = new byte[8192];
        int leidos;
        while ((leidos = await origen.ReadAsync(bloque)) > 0)
        {
            if (buffer.Length + leidos > MaximoBytes) throw new InvalidOperationException("La foto no puede superar los 2 MB.");
            buffer.Write(bloque, 0, leidos);
        }
        var datos = buffer.ToArray();
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var png = datos.Length >= 24 && datos.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && datos.AsSpan(datos.Length - 12, 12).SequenceEqual(new byte[] { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
        var jpeg = datos.Length >= 4 && datos[0] == 255 && datos[1] == 216 && datos[2] == 255
            && datos[^2] == 255 && datos[^1] == 217;
        if (extension == ".png" && png) return new FotoValidada(datos, ".png");
        if ((extension == ".jpg" || extension == ".jpeg") && jpeg) return new FotoValidada(datos, ".jpg");
        throw new InvalidOperationException("Seleccioná una imagen PNG o JPG válida. No se admiten SVG ni otros formatos.");
    }

    public async Task<string> GuardarAsync(FotoValidada foto)
    {
        Directory.CreateDirectory(_directorio);
        var nombre = Guid.NewGuid().ToString("N") + foto.Extension;
        try
        {
            await using var destino = new FileStream(Ruta(nombre), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await destino.WriteAsync(foto.Contenido);
            return nombre;
        }
        catch { Eliminar(nombre); throw; }
    }

    public string Ruta(string nombre)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(nombre, @"\A[a-f0-9]{32}\.(png|jpg)\z"))
            throw new InvalidOperationException("La referencia de la foto no es válida.");
        return Path.Combine(_directorio, nombre);
    }

    public void Eliminar(string? nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return;
        try { File.Delete(Ruta(nombre)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { _logger.LogWarning(error, "No se pudo quitar el archivo de foto {Nombre}.", nombre); }
    }
}
