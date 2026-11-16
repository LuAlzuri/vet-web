namespace VetWeb.Middleware;

public sealed class RegistroErroresMiddleware(RequestDelegate siguiente, ILogger<RegistroErroresMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (BadHttpRequestException) when (!contexto.Response.HasStarted)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al procesar {Metodo} {Ruta}. Solicitud {IdSolicitud}",
                contexto.Request.Method, contexto.Request.Path.Value, contexto.TraceIdentifier);
            if (!contexto.Request.Path.StartsWithSegments("/api") || contexto.Response.HasStarted) throw;

            contexto.Response.Clear();
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await contexto.Response.WriteAsJsonAsync(new { error = "Ocurrió un error interno." }, contexto.RequestAborted);
        }
    }
}
