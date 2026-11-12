using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Dtos;
using VetWeb.Services.Chatbot;

namespace VetWeb.Controllers.Api;

[ApiController, Route("api/chatbot"), Authorize(Roles = "Veterinario,Tutor,Administrador")]
public sealed class ChatbotApiController(IChatbotProveedor proveedor) : ControllerBase
{
    [HttpPost("mensajes"), RequestSizeLimit(65536)]
    public async Task<ActionResult<ChatbotRespuesta>> Enviar(ChatbotSolicitud solicitud, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await proveedor.ResponderAsync(solicitud, cancellationToken));
    }
}
