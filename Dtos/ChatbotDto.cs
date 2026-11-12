using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public sealed class ChatbotSolicitud : IValidatableObject
{
    [Required, StringLength(2000)]
    public string Mensaje { get; set; } = "";

    [MaxLength(12)]
    public List<ChatbotMensaje> Historial { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(Mensaje))
            yield return new ValidationResult("Escribí una consulta.", [nameof(Mensaje)]);
        if (Historial is null || Historial.Any(x => x is null ||
            (x.Rol != "user" && x.Rol != "assistant") || string.IsNullOrWhiteSpace(x.Texto) || x.Texto.Length > 4000))
            yield return new ValidationResult("El historial no es válido.", [nameof(Historial)]);
    }
}

public sealed record ChatbotMensaje(string Rol, string Texto);
public sealed record ChatbotRespuesta(string Mensaje, bool Conectado);
