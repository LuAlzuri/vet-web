using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class CuentaDto
{
    public int Id { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;
    [Required(ErrorMessage = "El apellido es obligatorio."), StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
    [Display(Name = "Apellido")]
    public string Apellido { get; set; } = string.Empty;
    [Required(ErrorMessage = "El correo es obligatorio."), EmailAddress(ErrorMessage = "Ingresá un correo válido.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;
    [DataType(DataType.Password), StringLength(128, ErrorMessage = "Máximo 128 caracteres.")]
    [Display(Name = "Contraseña inicial")]
    public string? Password { get; set; }
    [Display(Name = "Vincular una cuenta existente con este correo")]
    public bool VincularExistente { get; set; }
    [Range(1, 99999, ErrorMessage = "La matrícula debe estar entre 1 y 99999.")]
    [Display(Name = "Matrícula")]
    public int? Matricula { get; set; }
}
