namespace VetWeb.Dtos;

public record TurnoRespuestaDto(int Id, int PerroId, string Perro, int CupoDiarioId, DateTime Fecha,
    int CantidadReservada, int? VeterinarioId, int EstadoId, string Estado, string? Observaciones);
