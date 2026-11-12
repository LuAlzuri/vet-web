using VetWeb.Dtos;

namespace VetWeb.Services.Chatbot;

public sealed class ChatbotSinConexion : IChatbotProveedor
{
    public Task<ChatbotRespuesta> ResponderAsync(ChatbotSolicitud solicitud, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ChatbotRespuesta(
            "El asistente está en preparación. Todavía no está conectado a Gemini y no puede responder consultas. Podés seguir usando las opciones de VeteWeb.", false));
    }
}
