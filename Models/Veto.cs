using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class Veto
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un tutor válido.")]
    [Display(Name = "Tutor")]
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un veterinario válido.")]
    [Display(Name = "Veterinario")]
    public int? VeterinarioId { get; set; }
    public Veterinario? Veterinario { get; set; }

    [Display(Name = "Fecha de inicio")]
    public DateTime FechaInicio { get; set; }

    [StringLength(1000, ErrorMessage = "El motivo no puede superar los 1000 caracteres.")]
    [Display(Name = "Motivo")]
    public string? Motivo { get; set; }

    [Display(Name = "Fecha de fin")]
    public DateTime? FechaFin { get; set; }

    [StringLength(1000, ErrorMessage = "El motivo de finalización no puede superar los 1000 caracteres.")]
    [Display(Name = "Motivo de finalización")]
    public string? MotivoFinVeto { get; set; }
}
