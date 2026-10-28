using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class PerroPatologia
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un perro válido.")]
    [Display(Name = "Perro")]
    public int PerroId { get; set; }

    public Perro Perro { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una patología válida.")]
    [Display(Name = "Patología")]
    public int PatologiaId { get; set; }

    public Patologia Patologia { get; set; } = null!;

    [Display(Name = "Controlado")]
    public bool Controlado { get; set; }

    [StringLength(1000, ErrorMessage = "La observación no puede superar los 1000 caracteres.")]
    [Display(Name = "Observación")]
    public string? Observacion { get; set; }
}
