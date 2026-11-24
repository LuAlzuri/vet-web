using System.Text.Json;
using System.Text.RegularExpressions;
using VetWeb.Dtos;

namespace VetWeb.Services;

public sealed class ConsultaErroresService(IConfiguration configuracion, IWebHostEnvironment entorno)
{
    internal static string ObtenerRuta(IConfiguration configuracion, IWebHostEnvironment entorno)
    {
        var ruta = Path.GetFullPath(configuracion["ErrorLog:Path"] ?? "App_Data/logs/errores-.log", entorno.ContentRootPath);
        var raizPublica = entorno.WebRootPath ?? Path.Combine(entorno.ContentRootPath, "wwwroot");
        var relativa = Path.GetRelativePath(raizPublica, ruta);
        if (!Path.IsPathRooted(relativa) && relativa != ".." &&
            !relativa.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("El registro de errores debe guardarse fuera de wwwroot.");
        return ruta;
    }

    public async Task<List<ErrorLogRespuesta>> ListarAsync(
        DateOnly? desde, DateOnly? hasta, int pagina, int tamano, CancellationToken ct)
    {
        var limite = pagina * tamano;
        var seleccion = new PriorityQueue<ErrorLogRespuesta, (DateTimeOffset Fecha, long Orden)>();
        long orden = 0;
        await LeerAsync(desde, hasta, entrada =>
        {
            seleccion.Enqueue(entrada, (entrada.FechaUtc, orden++));
            if (seleccion.Count > limite)
                seleccion.Dequeue();
        }, ct);
        return seleccion.UnorderedItems.OrderByDescending(e => e.Priority)
            .Skip((pagina - 1) * tamano).Select(e => e.Element).ToList();
    }

    internal async Task<List<(string Mensaje, string? Origen, int Cantidad)>> MasFrecuentesAsync(
        DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var cantidades = new Dictionary<(string Mensaje, string? Origen), int>();
        await LeerAsync(desde, hasta, entrada =>
        {
            var clave = (entrada.Mensaje, entrada.Origen);
            cantidades[clave] = cantidades.GetValueOrDefault(clave) + 1;
        }, ct);

        var maximo = cantidades.Values.DefaultIfEmpty().Max();
        return cantidades.Where(x => x.Value == maximo)
            .OrderBy(x => x.Key.Mensaje, StringComparer.Ordinal).ThenBy(x => x.Key.Origen, StringComparer.Ordinal)
            .Select(x => (x.Key.Mensaje, x.Key.Origen, x.Value)).ToList();
    }

    private async Task LeerAsync(DateOnly? desde, DateOnly? hasta, Action<ErrorLogRespuesta> registrar, CancellationToken ct)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value > hasta.Value)
            throw new InvalidOperationException("La fecha inicial no puede ser posterior a la fecha final.");

        var ruta = ObtenerRuta(configuracion, entorno);
        var carpeta = new DirectoryInfo(Path.GetDirectoryName(ruta)!);
        if (!carpeta.Exists)
            return;

        var prefijo = Regex.Escape(Path.GetFileNameWithoutExtension(ruta));
        var extension = Regex.Escape(Path.GetExtension(ruta));
        var patron = new Regex(@"\A" + prefijo + @"[0-9]{8}(?:_[0-9]{3,})?" + extension + @"\z", RegexOptions.CultureInvariant);
        try
        {
            foreach (var archivo in carpeta.EnumerateFiles().Where(f => patron.IsMatch(f.Name))
                         .OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                if ((archivo.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                FileStream flujo;
                try
                {
                    flujo = new FileStream(archivo.FullName, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
                }
                catch (FileNotFoundException)
                {
                    // La rotación puede quitar el archivo después de enumerarlo.
                    continue;
                }

                using var lector = new StreamReader(flujo);
                while (await lector.ReadLineAsync(ct) is { } linea)
                {
                    var entrada = LeerEntrada(linea);
                    if (entrada is null)
                        continue;
                    if (desde.HasValue || hasta.HasValue)
                    {
                        var dia = DateOnly.FromDateTime(Reloj.EnArgentina(entrada.FechaUtc.UtcDateTime));
                        if (dia < desde || dia > hasta)
                            continue;
                    }

                    registrar(entrada);
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // La carpeta puede desaparecer entre la comprobación y la enumeración.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException("No se pudo leer el registro de errores.", ex);
        }

    }

    private static ErrorLogRespuesta? LeerEntrada(string linea)
    {
        try
        {
            using var documento = JsonDocument.Parse(linea);
            var evento = documento.RootElement;
            if (evento.ValueKind != JsonValueKind.Object
                || !evento.TryGetProperty("Timestamp", out var marca) || marca.ValueKind != JsonValueKind.String
                || !marca.TryGetDateTimeOffset(out var fecha))
                return null;

            var nivel = Texto(evento, "Level");
            var mensaje = Texto(evento, "MessageTemplate");
            if (nivel is not ("Error" or "Fatal") || mensaje is null)
                return null;

            evento.TryGetProperty("Properties", out var propiedades);
            var origen = Texto(propiedades, "SourceContext");
            if (origen == typeof(Chatbot.GeminiChatbotProveedor).FullName
                && mensaje is Chatbot.GeminiChatbotProveedor.MensajeErrorHttp or Chatbot.GeminiChatbotProveedor.MensajeErrorHttpConEstado
                && propiedades.TryGetProperty("Estado", out var estado)
                && estado.ValueKind == JsonValueKind.Number && estado.TryGetInt32(out var codigo)
                && codigo is >= 100 and <= 599)
            {
                var estadoGoogle = Texto(propiedades, "EstadoGoogle");
                mensaje = mensaje.Replace("{Estado}", codigo.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Replace("{EstadoGoogle}", Chatbot.GeminiChatbotProveedor.EsEstadoGoogle(estadoGoogle)
                        ? estadoGoogle! : "sin estado estructurado reconocido");
            }

            return new ErrorLogRespuesta(fecha.ToUniversalTime(), nivel, mensaje, origen,
                Texto(propiedades, "Metodo"), Texto(propiedades, "Ruta"), Texto(propiedades, "IdSolicitud"));
        }
        catch (JsonException)
        {
            // Una línea parcial o dañada no impide consultar las demás.
            return null;
        }
    }

    private static string? Texto(JsonElement objeto, string nombre) =>
        objeto.ValueKind == JsonValueKind.Object && objeto.TryGetProperty(nombre, out var propiedad)
            && propiedad.ValueKind == JsonValueKind.String ? propiedad.GetString() : null;
}
