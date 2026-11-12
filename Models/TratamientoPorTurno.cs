using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class TratamientoPorTurno
{
    public int TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;
    public int TratamientoId { get; set; }
    public Tratamiento Tratamiento { get; set; } = null!;

    [StringLength(1000)]
    public string? Razon { get; set; }
}
