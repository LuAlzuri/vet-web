using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class ServicioPorTurno
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un turno válido.")]
    [Display(Name = "Turno")]
    public int TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un servicio válido.")]
    [Display(Name = "Servicio")]
    public int ServicioId { get; set; }
    public Servicio Servicio { get; set; } = null!;

    [Display(Name = "Completado")]
    public bool Completado { get; set; }
}
