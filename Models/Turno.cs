using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class Turno
{
    public int Id { get; set; }

    [StringLength(2000, ErrorMessage = "Las observaciones no pueden superar los 2000 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Observaciones { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un veterinario válido.")]
    [Display(Name = "Veterinario")]
    public int VeterinarioId { get; set; }
    public Veterinario Veterinario { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un perro válido.")]
    [Display(Name = "Perro")]
    public int PerroId { get; set; }
    public Perro Perro { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un cupo válido.")]
    [Display(Name = "Cupo diario")]
    public int CupoDiarioId { get; set; }
    public CupoDiario CupoDiario { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un estado válido.")]
    [Display(Name = "Estado")]
    public int EstadoId { get; set; }
    public Estado Estado { get; set; } = null!;

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un tratamiento válido.")]
    [Display(Name = "Tratamiento")]
    public int? TratamientoId { get; set; }
    public Tratamiento? Tratamiento { get; set; }
}
