using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class TurnoDto : IValidatableObject
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Perro")]
    public int PerroId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Veterinario")]
    public int? VeterinarioId { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Cupo diario")]
    public int CupoDiarioId { get; set; }
    [Display(Name = "Tratamiento")]
    public int? TratamientoId { get; set; }
    [StringLength(2000, ErrorMessage = "Máximo 2000 caracteres."), Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }
    [Display(Name = "Servicios solicitados")]
    public List<int> ServicioIds { get; set; } = new();
    [StringLength(100, ErrorMessage = "Máximo 100 caracteres."), Display(Name = "Nuevo tratamiento")]
    public string? NuevoTratamiento { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TratamientoId == -1 && string.IsNullOrWhiteSpace(NuevoTratamiento))
            yield return new ValidationResult("Escribí la nueva opción.", new[] { nameof(NuevoTratamiento) });
    }
}
