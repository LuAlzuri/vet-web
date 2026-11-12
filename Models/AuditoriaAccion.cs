using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class AuditoriaAccion
{
    public const string TurnoSolicitado = "TurnoSolicitado";
    public const string ReservaPresencial = "ReservaPresencial";
    public const string TurnoEditado = "TurnoEditado";
    public const string EstadoTurnoCambiado = "EstadoTurnoCambiado";
    public const string VetoAutomaticoAplicado = "VetoAutomaticoAplicado";
    public const string VetoAplicado = "VetoAplicado";
    public const string VetoLevantado = "VetoLevantado";

    public long Id { get; set; }
    public DateTime FechaUtc { get; set; }
    public string UsuarioId { get; set; } = null!;
    public ApplicationUser Usuario { get; set; } = null!;
    public int TutorId { get; set; }
    public Tutor Tutor { get; set; } = null!;

    [MaxLength(30)]
    public string RolActivo { get; set; } = string.Empty;

    [MaxLength(40)]
    public string Accion { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Entidad { get; set; } = string.Empty;

    public int EntidadId { get; set; }
    public int? EstadoAnteriorId { get; set; }
    public int? EstadoNuevoId { get; set; }
}
