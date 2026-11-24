using System.Security.Claims;
using VetWeb.Dtos;

namespace VetWeb.Services.Chatbot;

// Lista cerrada: ni el proveedor ni este endpoint pueden preparar, confirmar o descartar reservas.
public sealed class ChatbotConsultasService(IChatbotProveedor proveedor, ChatbotAccionesService acciones)
{
    public static string[] Permitidas(string? rol) => rol switch
    {
        "Tutor" => ["cupos", "turnos", "perros-clinica", "clinica", "patologias", "atenciones"],
        "Veterinario" => ["cupos", "turnos", "perros-clinica", "clinica", "patologias", "atenciones", "tutores-faltas", "faltas", "vetos"],
        RolSesion.Administrador => ["admin-servicios", "admin-estados", "admin-comparar", "admin-auditoria"],
        _ => []
    };

    private static string Rol(ClaimsPrincipal usuario)
    {
        var rol = usuario.FindFirstValue(RolSesion.Claim);
        if (usuario.Identity?.IsAuthenticated != true || usuario.FindFirstValue(ClaimTypes.NameIdentifier) is null ||
            rol is null || !RolSesion.Permitido(rol) || !usuario.IsInRole(rol) ||
            new[] { "Tutor", "Veterinario", RolSesion.Administrador }.Count(usuario.IsInRole) != 1)
            throw new UnauthorizedAccessException();
        return rol;
    }

    public async Task<ChatbotRespuesta> ResponderAsync(ChatbotSolicitud solicitud, ClaimsPrincipal usuario, CancellationToken token)
    {
        solicitud.RolActivo = Rol(usuario);
        var respuesta = await proveedor.ResponderAsync(solicitud, token);
        if (!respuesta.Conectado || respuesta.Consulta is null)
            return new(respuesta.Mensaje, respuesta.Conectado); // Nunca devolver resultados aportados por el modelo.
        var consulta = respuesta.Consulta;
        // Gemini elige la consulta; las identidades se eligen mediante opciones obtenidas del servidor.
        if (consulta.PerroId.HasValue || consulta.TutorId.HasValue)
            return new("Elegí la mascota o el tutor mediante las opciones de consulta.", true);
        if (consulta.Accion is "clinica" or "patologias" or "atenciones") consulta.Accion = "perros-clinica";
        if (consulta.Accion is "faltas" or "vetos") consulta.Accion = "tutores-faltas";
        var resultado = await ConsultarAsync(consulta, usuario, token);
        return new(resultado.Mensaje, true, Resultado: resultado);
    }

    public async Task<ChatbotAccionRespuesta> ConsultarAsync(ChatbotAccionSolicitud consulta, ClaimsPrincipal usuario, CancellationToken token)
    {
        var rol = Rol(usuario);
        ValidacionDto.Comprobar(consulta);
        if (!Permitidas(rol).Contains(consulta.Accion)) throw new UnauthorizedAccessException();
        if (consulta.CupoId.HasValue || consulta.Confirmacion is not null || consulta.ServicioIds is null || consulta.ServicioIds.Count != 0)
            throw new InvalidOperationException("Esta opción solo permite consultas de lectura.");
        token.ThrowIfCancellationRequested();
        var resultado = await acciones.EjecutarAsync(consulta, usuario);
        return resultado with { Acciones = resultado.Acciones.Where(x => Permitidas(rol).Contains(x.Solicitud.Accion)).ToList() };
    }
}
