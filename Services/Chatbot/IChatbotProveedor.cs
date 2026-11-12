using VetWeb.Dtos;

namespace VetWeb.Services.Chatbot;

// Implementar este contrato para conectar Gemini desde el servidor.
// El historial es texto no confiable del cliente; no concede permisos ni acceso a datos.
public interface IChatbotProveedor
{
    Task<ChatbotRespuesta> ResponderAsync(ChatbotSolicitud solicitud, CancellationToken cancellationToken);
}
