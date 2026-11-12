using System.ComponentModel.DataAnnotations;
namespace VetWeb.Dtos;
public record PerroRespuestaDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = "";
    public DateTime FechaNacimiento { get; init; }
    public int TutorId { get; init; }
    public int GrupoEtarioId { get; init; }
    public int TamanioId { get; init; }
    public bool Reactivo { get; init; }
    public bool CalendarioVacunasCompleto { get; init; }
    public bool TieneImagen { get; init; }
}
public record PerfilRespuestaDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = "";
    public string Apellido { get; init; } = "";
    public string Email { get; init; } = "";
    public int? Matricula { get; init; }
}
public record PatologiaPerroRespuestaDto(int PatologiaId, string Descripcion, bool Controlado, string? Observacion);
public record ServicioTurnoRespuestaDto(int ServicioId, string Descripcion, bool Completado);
public class TelefonoSolicitudDto
{
    [Required, StringLength(100)] public string NombrePropietario { get; set; } = "";
    [Required, StringLength(30), Phone] public string Numero { get; set; } = "";
}
public class PatologiaSolicitudDto
{
    public int PatologiaId { get; set; }
    public bool Controlado { get; set; }
    [StringLength(1000)] public string? Observacion { get; set; }
    [StringLength(100)] public string? NuevaPatologia { get; set; }
}
public class ServicioTurnoSolicitudDto
{
    public int ServicioId { get; set; }
    public bool Completado { get; set; }
}
