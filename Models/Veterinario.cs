using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class Veterinario
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [Display(Name = "Usuario")]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser Usuario { get; set; } = null!;

    [Range(1, 99999, ErrorMessage = "La matrícula debe ser un número entre 1 y 99999.")]
    [Display(Name = "Matrícula")]
    public int Matricula { get; set; }

    [Display(Name = "Eliminado")]
    public bool Eliminado { get; set; }
}
