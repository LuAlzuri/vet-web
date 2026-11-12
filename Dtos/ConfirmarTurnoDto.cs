using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public class ConfirmarTurnoDto
{
    public int Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Reservá al menos una plaza.")]
    [Display(Name = "Plazas a reservar")]
    public int? Cantidad { get; set; }
}
