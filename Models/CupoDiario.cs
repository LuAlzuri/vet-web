using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class CupoDiario
{
    public int Id { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Fecha del cupo")]
    public DateTime FechaCupo { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "La cantidad disponible no puede ser negativa.")]
    [Display(Name = "Cupos disponibles")]
    public int NumeroCuposDisponibles { get; set; }
}
