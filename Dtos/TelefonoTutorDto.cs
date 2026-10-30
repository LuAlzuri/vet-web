using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class TelefonoTutorDto
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Tutor")]
    public int TutorId { get; set; }
    [Required(ErrorMessage = "El propietario es obligatorio."), StringLength(100, ErrorMessage = "Máximo 100 caracteres."), Display(Name = "Propietario")]
    public string NombrePropietario { get; set; } = string.Empty;
    [Required(ErrorMessage = "El teléfono es obligatorio."), StringLength(30, ErrorMessage = "Máximo 30 caracteres."), Phone(ErrorMessage = "Ingresá un teléfono válido."), Display(Name = "Teléfono")]
    public string Numero { get; set; } = string.Empty;
}
