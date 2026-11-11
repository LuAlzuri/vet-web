using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class Perro
{
    public int Id { get; set; }

    [StringLength(40)]
    public string? FotoArchivo { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    [Display(Name = "Nombre")]
    public string Nombre { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Fecha de nacimiento")]
    public DateTime FechaNacimiento { get; set; }

    [Display(Name = "Reactivo")]
    public bool Reactivo { get; set; }

    [Display(Name = "Calendario de vacunas completo")]
    public bool CalendarioVacunasCompleto { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un tutor válido.")]
    [Display(Name = "Tutor")]
    public int TutorId { get; set; }

    public Tutor Tutor { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un grupo etario válido.")]
    [Display(Name = "Grupo etario")]
    public int GrupoEtarioId { get; set; }

    public GrupoEtario GrupoEtario { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un tamaño válido.")]
    [Display(Name = "Tamaño")]
    public int TamanioId { get; set; }

    public Tamanio Tamanio { get; set; } = null!;

    public ICollection<PerroPatologia> Patologias { get; set; } = new List<PerroPatologia>();

    [Display(Name = "Eliminado")]
    public bool Eliminado { get; set; }
}
