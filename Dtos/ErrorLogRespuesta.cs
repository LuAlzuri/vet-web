namespace VetWeb.Dtos;

public sealed record ErrorLogRespuesta(DateTimeOffset FechaUtc, string Nivel, string Mensaje,
    string? Origen, string? Metodo, string? Ruta, string? IdSolicitud);
