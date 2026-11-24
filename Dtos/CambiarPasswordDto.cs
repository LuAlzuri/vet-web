using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public class CambiarPasswordDto
{
    [Required(ErrorMessage = "Ingresá tu contraseña actual.")]
    [DataType(DataType.Password), Display(Name = "Contraseña actual")]
    public string Actual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá la nueva contraseña.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Usá entre 8 y 128 caracteres.")]
    [DataType(DataType.Password), Display(Name = "Nueva contraseña")]
    public string Nueva { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirmá la nueva contraseña.")]
    [Compare(nameof(Nueva), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password), Display(Name = "Confirmar contraseña")]
    public string Confirmacion { get; set; } = string.Empty;
}
