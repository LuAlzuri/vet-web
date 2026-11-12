using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public sealed class ChatbotAccionSolicitud
{
    [Required, StringLength(40)] public string Accion { get; set; } = "inicio";
    [Range(1, int.MaxValue)] public int? PerroId { get; set; }
    [Range(1, int.MaxValue)] public int? TutorId { get; set; }
    [Range(1, int.MaxValue)] public int? CupoId { get; set; }
    [Range(1, 100000)] public int Pagina { get; set; } = 1;
    [StringLength(100)] public string? Busqueda { get; set; }
    [MaxLength(20)] public List<int> ServicioIds { get; set; } = [];
    [Required, RegularExpression("^(mes-actual|mes-anterior)$")] public string Periodo { get; set; } = "mes-actual";
    [StringLength(100)] public string? Confirmacion { get; set; }
}

public sealed record ChatbotTabla(string Titulo, string[] Columnas, List<string[]> Filas);
public sealed record ChatbotAccion(string Etiqueta, ChatbotAccionSolicitud Solicitud);
public sealed record ChatbotAccionRespuesta(string Mensaje, List<ChatbotTabla> Tablas,
    List<ChatbotAccion> Acciones, bool BuscarTutor = false);
