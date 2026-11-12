using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public class CompletarTurnoDto
{
    public int Id { get; set; }
    public List<int> ServicioIdsCompletados { get; set; } = new();
    public List<TratamientoAtencionDto> Tratamientos { get; set; } = new();

    [StringLength(2000, ErrorMessage = "Las observaciones no pueden superar los 2000 caracteres.")]
    public string? Observaciones { get; set; }
}

public class TratamientoAtencionDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un tratamiento válido.")]
    public int TratamientoId { get; set; }

    [StringLength(1000, ErrorMessage = "El motivo de cada tratamiento no puede superar los 1000 caracteres.")]
    public string? Razon { get; set; }
}

public record TratamientoAtencionRespuestaDto(int TratamientoId, string Descripcion, string? Razon);
public record AtencionRespuestaDto(int TurnoId, int EstadoId, DateTime? FinalizacionUtc, string? Observaciones,
    IReadOnlyList<int> ServicioIdsCompletados, IReadOnlyList<TratamientoAtencionRespuestaDto> Tratamientos);
