using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class CupoDiarioDto
{
    public int Id { get; set; }
    [Required(ErrorMessage = "La fecha es obligatoria."), DataType(DataType.Date), Display(Name = "Fecha")]
    public DateTime? FechaCupo { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(0, int.MaxValue, ErrorMessage = "La disponibilidad no puede ser negativa."), Display(Name = "Cupos disponibles")]
    public int NumeroCuposDisponibles { get; set; }
}
