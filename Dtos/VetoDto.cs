using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public class MotivoVetoDto
{
    [Required(ErrorMessage = "Ingresá el motivo.")]
    [StringLength(1000, ErrorMessage = "El motivo no puede superar los 1000 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

public record VetoDto(int Id, int TutorId, int? VeterinarioId, DateTime FechaInicio, DateTime? FechaFin,
    string? Motivo, string? MotivoFinVeto, bool Vigente);
