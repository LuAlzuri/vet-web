using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class Tratamiento
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(100, ErrorMessage = "La descripción no puede superar los 100 caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;
}
