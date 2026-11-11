using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public class PerroDto
{
    public int Id { get; set; }
    [Display(Name = "Foto del perro")]
    public IFormFile? Foto { get; set; }
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public bool TieneFoto { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio."), StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;
    [Required(ErrorMessage = "La fecha de nacimiento es obligatoria."), DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateTime? FechaNacimiento { get; set; }
    [Display(Name = "Reactivo")]
    public bool Reactivo { get; set; }
    [Display(Name = "Calendario de vacunas completo")]
    public bool CalendarioVacunasCompleto { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Tutor")]
    public int TutorId { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Grupo etario")]
    public int GrupoEtarioId { get; set; }
    [Required(ErrorMessage = "Este campo es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una opción válida."), Display(Name = "Tamaño")]
    public int TamanioId { get; set; }
}
