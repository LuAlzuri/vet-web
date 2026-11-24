using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VetWeb.Dtos;
using VetWeb.Services.Chatbot;

internal static class GeminiProveedorPruebas
{
    private sealed class Transporte(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> enviar) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => enviar(request, cancellationToken);
    }

    private sealed class Registro : ILogger<GeminiChatbotProveedor>
    {
        public IReadOnlyDictionary<string, object?> Campos { get; private set; } = new Dictionary<string, object?>();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel nivel, EventId id, TState estado, Exception? error, Func<TState, Exception?, string> formato)
        {
            if (estado is IEnumerable<KeyValuePair<string, object?>> campos)
                Campos = campos.ToDictionary();
        }
    }

    public static async Task EjecutarAsync()
    {
        static void Exigir(bool condicion, string mensaje) { if (!condicion) throw new Exception("Gemini: " + mensaje); }
        var valores = new Dictionary<string, string?> { ["Gemini:ApiKey"] = "clave-ficticia", ["Gemini:Modelo"] = "gemini-3.1-flash-lite", ["Gemini:TimeoutSegundos"] = "30" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var solicitud = new ChatbotSolicitud { Mensaje = "¿Cómo solicito turno?", Historial = [new("user", "Hola"), new("assistant", "Hola, ¿en qué te ayudo?")] };
        using var http = new HttpClient(new Transporte(async (request, token) =>
        {
            Exigir(request.RequestUri!.Host == "generativelanguage.googleapis.com" && request.RequestUri.AbsolutePath.EndsWith("gemini-3.1-flash-lite:generateContent"), "destino incorrecto");
            Exigir(!request.RequestUri.Query.Contains("clave") && request.Headers.GetValues("x-goog-api-key").Single() == "clave-ficticia", "la clave no se envía por encabezado");
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var contenido = json.RootElement.GetProperty("contents");
            Exigir(contenido.GetArrayLength() == 3 && contenido[1].GetProperty("role").GetString() == "model", "historial incorrecto");
            Exigir(contenido[2].GetProperty("parts")[0].GetProperty("text").GetString() == solicitud.Mensaje, "mensaje duplicado o perdido");
            Exigir(json.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!.Contains("No tenés acceso a datos reales"), "faltan instrucciones del servidor");
            Exigir(!json.RootElement.TryGetProperty("tools", out _), "se habilitaron herramientas en la conversación básica");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"candidates":[{"finishReason":"STOP","content":{"parts":[{"thought":true,"text":"Interno"},{"text":"Usá las acciones disponibles."}]}}]}""") };
        }));
        var proveedor = new GeminiChatbotProveedor(http, config, NullLogger<GeminiChatbotProveedor>.Instance);
        var respuesta = await proveedor.ResponderAsync(solicitud, CancellationToken.None);
        Exigir(respuesta.Conectado && respuesta.Mensaje == "Usá las acciones disponibles.", "no se extrajo el texto o se expuso razonamiento");
        foreach (var rol in new[] { "Tutor", "Veterinario", "Administrador" })
        {
            solicitud.RolActivo = rol;
            var accion = rol == "Administrador" ? "admin-estados" : "turnos";
            using var transporte = new HttpClient(new Transporte(async (request, token) =>
            {
                using var cuerpo = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                var funciones = cuerpo.RootElement.GetProperty("tools")[0].GetProperty("functionDeclarations");
                var funcion = funciones[0];
                var nombres = funciones.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToArray();
                Exigir(nombres.SequenceEqual(rol == "Administrador" ? ["consultar_vetweb"] :
                    new[] { "consultar_vetweb", "iniciar_reserva_vetweb" }), "se expuso una herramienta fuera del rol o de la selección");
                var opciones = funcion.GetProperty("parameters").GetProperty("properties").GetProperty("accion").GetProperty("enum").EnumerateArray().Select(x => x.GetString()).ToArray();
                Exigir(opciones.Contains(accion) && !opciones.Contains("preparar") && !opciones.Contains("confirmar"), "herramientas de escritura expuestas");
                Exigir(rol == "Administrador" ? !opciones.Contains("turnos") : !opciones.Contains("admin-auditoria"), "se mezclaron herramientas de roles");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { functionCall = new { name = "consultar_vetweb", args = new { accion } } } } } } } })) };
            }));
            var elegida = await new GeminiChatbotProveedor(transporte, config, NullLogger<GeminiChatbotProveedor>.Instance).ResponderAsync(solicitud, CancellationToken.None);
            Exigir(elegida.Conectado && elegida.Consulta?.Accion == accion && elegida.Resultado is null, "la función no se interpretó correctamente");
        }
        async Task<ChatbotRespuesta> Llamada(string rol, string herramienta, object argumentos)
        {
            solicitud.RolActivo = rol;
            var llamada = new { functionCall = new { name = herramienta, args = argumentos } };
            using var transporte = new HttpClient(new Transporte((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { llamada } } } }
                }))
            })));
            return await new GeminiChatbotProveedor(transporte, config, NullLogger<GeminiChatbotProveedor>.Instance)
                .ResponderAsync(solicitud, CancellationToken.None);
        }
        var reserva = await Llamada("Tutor", "iniciar_reserva_vetweb", new { mascota = "Luna", servicio = "Baño", dia = "mañana" });
        Exigir(reserva.Conectado && reserva.Reserva == new ChatbotPistasReserva { Mascota = "Luna", Servicio = "Baño", Dia = "mañana" }
            && reserva.Consulta is null, "no separó la intención de reserva de la consulta");
        foreach (var rechazada in new[] {
            await Llamada("Tutor", "iniciar_reserva_vetweb", new { perroId = 123 }),
            await Llamada("Veterinario", "iniciar_reserva_vetweb", new { confirmar = "token" }),
            await Llamada("Tutor", "confirmar", new { confirmacion = "token" }),
            await Llamada("Administrador", "iniciar_reserva_vetweb", new { mascota = "Luna" })
        })
            Exigir(!rechazada.Conectado && rechazada.Reserva is null, "se aceptaron identificadores, confirmación o un rol no permitido");
        solicitud.RolActivo = null;

        foreach (var (estado, cuerpo, esperado) in new[]
        {
            (HttpStatusCode.Forbidden, "clave-privada", "acceso"),
            (HttpStatusCode.NotFound, "detalle-interno", "modelo"),
            (HttpStatusCode.TooManyRequests, "detalle-interno", "límite"),
            (HttpStatusCode.ServiceUnavailable, "detalle-interno", "disponible"),
            (HttpStatusCode.OK, "no es json", "interpretar"),
            (HttpStatusCode.OK, "{}", "utilizable"),
            (HttpStatusCode.OK, """{"candidates":[{"finishReason":"SAFETY","content":{"parts":[{"text":"No mostrar"}]}}]}""", "utilizable")
        })
        {
            using var transporte = new HttpClient(new Transporte((_, _) => Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent(cuerpo) })));
            var resultado = await new GeminiChatbotProveedor(transporte, config, NullLogger<GeminiChatbotProveedor>.Instance).ResponderAsync(solicitud, CancellationToken.None);
            Exigir(!resultado.Conectado && resultado.Mensaje.Contains(esperado) && !resultado.Mensaje.Contains("clave-privada") && !resultado.Mensaje.Contains("detalle-interno"), "manejo de error incorrecto");
        }
        foreach (var (cuerpo, esperado) in new (string Cuerpo, string? Estado)[]
        {
            ("""{"error":{"code":503,"status":"UNAVAILABLE","message":"detalle-privado"}}""", "UNAVAILABLE"),
            ("<html>detalle-privado</html>", null),
            ("""{"error":{"code":503,"status":"detalle-privado"}}""", null),
            ("""{"error":{"code":502,"status":"UNAVAILABLE"}}""", null),
            ("""{"error":{"code":503,"status":"UNAVAILABLE"}}""" + new string(' ', 8192), null)
        })
        {
            var registro = new Registro();
            using var transporte = new HttpClient(new Transporte((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(cuerpo)
            })));
            var resultado = await new GeminiChatbotProveedor(transporte, config, registro).ResponderAsync(solicitud, CancellationToken.None);
            Exigir(!resultado.Conectado && Equals(registro.Campos["Estado"], 503)
                && Equals(registro.Campos["EstadoGoogle"], esperado)
                && registro.Campos.Values.All(x => !Convert.ToString(x)!.Contains("detalle-privado")), "diagnóstico HTTP incorrecto o con contenido privado");
        }
        foreach (var error in new Exception[] { new HttpRequestException("secreto"), new TaskCanceledException() })
        {
            using var transporte = new HttpClient(new Transporte((_, _) => Task.FromException<HttpResponseMessage>(error)));
            var resultado = await new GeminiChatbotProveedor(transporte, config, NullLogger<GeminiChatbotProveedor>.Instance).ResponderAsync(solicitud, CancellationToken.None);
            Exigir(!resultado.Conectado && !resultado.Mensaje.Contains("secreto"), "no se controló la conexión o el timeout");
        }
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        try { await proveedor.ResponderAsync(solicitud, cancelacion.Token); throw new Exception("No se propagó la cancelación del usuario."); }
        catch (OperationCanceledException) { }
        config["Gemini:Modelo"] = "https://otro-destino.invalid";
        Exigir(!(await proveedor.ResponderAsync(solicitud, CancellationToken.None)).Conectado, "se admitió un destino arbitrario");
        config["Gemini:ApiKey"] = "";
        Exigir(!(await proveedor.ResponderAsync(solicitud, CancellationToken.None)).Conectado, "sin clave se hizo una llamada");
        Console.WriteLine("Gemini: solicitud REST, historial, instrucciones, clave privada, texto, bloqueo, errores, timeout y cancelación verificados con transporte simulado.");
    }
}
