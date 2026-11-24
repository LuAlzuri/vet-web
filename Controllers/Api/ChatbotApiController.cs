using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Dtos;
using VetWeb.Services.Chatbot;

namespace VetWeb.Controllers.Api;

[ApiController, Route("api/chatbot"), Authorize(Roles = "Veterinario,Tutor,Administrador")]
public sealed class ChatbotApiController(ChatbotConsultasService consultas, ChatbotAccionesService acciones) : ControllerBase
{
    [HttpPost("mensajes"), RequestSizeLimit(65536), Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("chatbot-texto")]
    public async Task<ActionResult<ChatbotRespuesta>> Enviar(ChatbotSolicitud solicitud, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await consultas.ResponderAsync(solicitud, User, cancellationToken));
    }
    [HttpPost("consultas"), RequestSizeLimit(16384)]
    public async Task<ActionResult<ChatbotAccionRespuesta>> Consultar(ChatbotAccionSolicitud solicitud, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await consultas.ConsultarAsync(solicitud, User, token));
    }
    [HttpPost("acciones"), Authorize(Roles = "Veterinario,Tutor,Administrador"), RequestSizeLimit(16384)]
    public async Task<ActionResult<ChatbotAccionRespuesta>> Accion(ChatbotAccionSolicitud solicitud, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await acciones.EjecutarAsync(solicitud, User, token));
    }
}
