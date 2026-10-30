using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class CatalogoDto
{
    public int Id { get; set; }
    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    [Display(Name = "Descripción")]
    public string Descripcion { get; set; } = string.Empty;
}
