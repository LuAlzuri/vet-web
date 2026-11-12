namespace VetWeb.Dtos;

public record AuditoriaDto(long Id, DateTime FechaUtc, string UsuarioId, string Usuario, string RolActivo,
    string Accion, string Entidad, int EntidadId, int TutorId, string Tutor, int? EstadoAnteriorId, int? EstadoNuevoId);

public record AuditoriaPaginaDto(IReadOnlyList<AuditoriaDto> Eventos, int Pagina, bool HaySiguiente);
