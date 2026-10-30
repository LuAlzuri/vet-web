using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class PerroPatologiaDto : IValidatableObject
{
    public bool EsEdicion { get; set; }
    public string ContextoPerro { get; set; } = string.Empty;
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Perro")]
    public int PerroId { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(-1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Patología")]
    public int PatologiaId { get; set; }
    [Display(Name = "Controlado")]
    public bool Controlado { get; set; }
    [StringLength(1000, ErrorMessage = "Máximo 1000 caracteres."), Display(Name = "Observación")]
    public string? Observacion { get; set; }
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres."), Display(Name = "Nueva patología")]
    public string? NuevaPatologia { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PatologiaId == -1 && string.IsNullOrWhiteSpace(NuevaPatologia))
            yield return new ValidationResult("Escribí la nueva opción.", new[] { nameof(NuevaPatologia) });
    }
}
