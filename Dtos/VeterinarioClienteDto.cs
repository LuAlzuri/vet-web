using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public class VeterinarioClienteDto
{
    [Display(Name = "Teléfono de contacto")]
    [StringLength(30, ErrorMessage = "Máximo 30 caracteres.")]
    [Phone(ErrorMessage = "Ingresá un número de teléfono válido.")]
    public string? Telefono { get; set; }
}
