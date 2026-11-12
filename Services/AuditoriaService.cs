using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Services;

public class AuditoriaService(ApplicationDbContext db)
{
    // El servicio que realiza la operación guarda el evento dentro de su transacción.
    public void RegistrarTurno(Turno turno, int tutorId, string usuarioId, bool esVeterinario,
        string accion, int? estadoAnteriorId = null)
    {
        db.AuditoriaAcciones.Add(new AuditoriaAccion
        {
            FechaUtc = DateTime.UtcNow,
            UsuarioId = usuarioId,
            TutorId = tutorId,
            RolActivo = esVeterinario ? "Veterinario" : "Tutor",
            Accion = accion,
            Entidad = nameof(Turno),
            EntidadId = turno.Id,
            EstadoAnteriorId = estadoAnteriorId,
            EstadoNuevoId = turno.EstadoId
        });
    }

    public void RegistrarVeto(Veto veto, string usuarioId, bool levantado)
    {
        db.AuditoriaAcciones.Add(new AuditoriaAccion
        {
            FechaUtc = levantado ? veto.FechaFin!.Value : veto.FechaInicio,
            UsuarioId = usuarioId,
            TutorId = veto.TutorId,
            RolActivo = "Veterinario",
            Accion = levantado ? AuditoriaAccion.VetoLevantado : AuditoriaAccion.VetoAplicado,
            Entidad = nameof(Veto),
            EntidadId = veto.Id
        });
    }

    public async Task<AuditoriaPaginaDto> ConsultarAsync(int pagina, int? tutorId)
    {
        const int tamano = 25;
        // El historial incluye las cuentas y los tutores que recibieron una baja lógica.
        var consulta = db.AuditoriaAcciones.IgnoreQueryFilters().AsNoTracking();
        if (tutorId.HasValue) consulta = consulta.Where(x => x.TutorId == tutorId.Value);
        var eventos = await consulta.OrderByDescending(x => x.FechaUtc).ThenByDescending(x => x.Id)
            .Skip((pagina - 1) * tamano).Take(tamano + 1)
            .Select(x => new AuditoriaDto(x.Id, x.FechaUtc, x.UsuarioId, x.Usuario.Nombre + " " + x.Usuario.Apellido,
                x.RolActivo, x.Accion, x.Entidad, x.EntidadId, x.TutorId, x.Tutor.Usuario.Nombre + " " + x.Tutor.Usuario.Apellido,
                x.EstadoAnteriorId, x.EstadoNuevoId))
            .ToListAsync();
        var haySiguiente = eventos.Count > tamano;
        if (haySiguiente) eventos.RemoveAt(tamano);
        return new AuditoriaPaginaDto(eventos, pagina, haySiguiente);
    }
}
