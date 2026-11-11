using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class ServicioPorTurnoDto
{
    public bool EsEdicion { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Turno")]
    public int TurnoId { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Servicio")]
    public int ServicioId { get; set; }
    [Display(Name = "Completado")]
    public bool Completado { get; set; }
}
