using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class TelefonoTutor
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un tutor válido.")]
    [Display(Name = "Tutor")]
    public int TutorId { get; set; }

    public Tutor Tutor { get; set; } = null!;

    [Required(ErrorMessage = "El nombre del propietario es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre del propietario no puede superar los 100 caracteres.")]
    [Display(Name = "Nombre del propietario")]
    public string NombrePropietario { get; set; } = string.Empty;

    [Required(ErrorMessage = "El número de teléfono es obligatorio.")]
    [StringLength(30, ErrorMessage = "El número de teléfono no puede superar los 30 caracteres.")]
    [Phone(ErrorMessage = "Ingresá un número de teléfono válido.")]
    [Display(Name = "Número de teléfono")]
    public string Numero { get; set; } = string.Empty;
}
