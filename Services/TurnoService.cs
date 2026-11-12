using System.Data;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
namespace VetWeb.Services;
public class TurnoService
{
    private readonly ApplicationDbContext _context;
    private readonly CatalogosFormularioService _catalogos;
    private readonly VetoService _vetos;
    private readonly AuditoriaService _auditoria;
    public TurnoService(ApplicationDbContext context, CatalogosFormularioService catalogos, VetoService vetos,
        AuditoriaService auditoria)
    { _context = context; _catalogos = catalogos; _vetos = vetos; _auditoria = auditoria; }

    public IQueryable<Turno> ConsultarVisibles(string usuarioId, bool esVeterinario)
    {
        var turnos = _context.Turnos.IgnoreQueryFilters().AsNoTracking();
        return esVeterinario
            ? turnos.Where(x => x.VeterinarioId == null || x.Veterinario!.UserId == usuarioId)
            : turnos.Where(x => x.Perro.Tutor.UserId == usuarioId);
    }

    public async Task<AtencionRespuestaDto?> ConsultarAtencionAsync(int id, string usuarioId, bool esVeterinario)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var turno = await ConsultarVisibles(usuarioId, esVeterinario).SingleOrDefaultAsync(x => x.Id == id);
        if (turno is null) return null;
        var fecha = await _context.AuditoriaAcciones.Where(x => x.Entidad == nameof(Turno) && x.EntidadId == id
                && x.Accion == AuditoriaAccion.EstadoTurnoCambiado && x.EstadoNuevoId == EstadosTurno.Completado)
            .Select(x => (DateTime?)x.FechaUtc).MinAsync();
        var servicios = await _context.ServiciosPorTurno.IgnoreQueryFilters().Where(x => x.TurnoId == id && x.Completado)
            .OrderBy(x => x.ServicioId).Select(x => x.ServicioId).ToListAsync();
        var tratamientos = await _context.TratamientosPorTurno.IgnoreQueryFilters().Where(x => x.TurnoId == id)
            .OrderBy(x => x.TratamientoId).Select(x => new TratamientoAtencionRespuestaDto(x.TratamientoId, x.Tratamiento.Descripcion, x.Razon)).ToListAsync();
        await transaction.CommitAsync();
        return new AtencionRespuestaDto(id, turno.EstadoId, fecha, turno.Observaciones, servicios, tratamientos);
    }

    public Task<int> GuardarAsync(TurnoDto dto, string usuarioId, bool esVeterinario) =>
        GuardarAsync(dto, usuarioId, esVeterinario, presencial: false);

    public Task<int> ReservarPresencialAsync(TurnoDto dto, string usuarioId) =>
        GuardarAsync(dto, usuarioId, esVeterinario: true, presencial: true);

    private async Task<int> GuardarAsync(TurnoDto dto, string usuarioId, bool esVeterinario, bool presencial)
    {
        if (!esVeterinario && (dto.TratamientoId is not null || !string.IsNullOrWhiteSpace(dto.NuevoTratamiento)))
            throw new InvalidOperationException("El tratamiento debe registrarlo el veterinario.");
        if (dto.Id != 0)
        {
            if (presencial) throw new InvalidOperationException("La reserva presencial debe ser un turno nuevo.");
            if (!esVeterinario) throw new InvalidOperationException("Solo el veterinario puede editar un turno solicitado.");
            await EditarAsync(dto, usuarioId);
            return dto.Id;
        }
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var perro = await _context.Perros.Include(x => x.Tutor).ThenInclude(x => x.Usuario).SingleOrDefaultAsync(x => x.Id == dto.PerroId);
        if (perro is null || !perro.Tutor.Usuario.Activo) throw new InvalidOperationException("El perro o su tutor no están disponibles.");
        if (!esVeterinario && perro.Tutor.UserId != usuarioId) throw new InvalidOperationException("Solo podés solicitar turnos para tus perros.");
        await _vetos.BloquearTutorAsync(perro.TutorId);
        var vetado = await _vetos.Vigentes().AnyAsync(x => x.TutorId == perro.TutorId);
        if (vetado && !presencial)
            throw new InvalidOperationException(esVeterinario
                ? "El tutor tiene un veto vigente. Usá la opción Reserva presencial."
                : "Tenés un veto vigente. Comunicate con la veterinaria para solicitar una reserva presencial.");
        if (presencial && !vetado)
            throw new InvalidOperationException("La reserva presencial está disponible para tutores con un veto vigente.");
        if (presencial)
            dto.VeterinarioId = await _vetos.ObtenerVeterinarioIdAsync(usuarioId);
        if (dto.VeterinarioId is not null && !await _context.Veterinarios.AnyAsync(x => x.Id == dto.VeterinarioId && x.Usuario.Activo)) throw new InvalidOperationException("Seleccioná un veterinario activo.");
        var cupo = await _context.CuposDiarios.FromSqlInterpolated($"SELECT * FROM \"CuposDiarios\" WHERE \"Id\" = {dto.CupoDiarioId} FOR UPDATE").SingleOrDefaultAsync();
        if (cupo is null || cupo.FechaCupo < Reloj.Hoy || cupo.NumeroCuposDisponibles <= 0) throw new InvalidOperationException("El cupo ya no está disponible. Actualizá la página.");
        if (perro.FechaNacimiento > cupo.FechaCupo || (cupo.FechaCupo - perro.FechaNacimiento).TotalDays < 45 && !perro.CalendarioVacunasCompleto)
            throw new InvalidOperationException("No se puede reservar: revisá la fecha de nacimiento y las vacunas del cachorro menor de 45 días.");
        if (await _context.Turnos.IgnoreQueryFilters().AnyAsync(x => x.PerroId == dto.PerroId && x.CupoDiario.FechaCupo == cupo.FechaCupo && (x.EstadoId == EstadosTurno.Solicitado || x.EstadoId == EstadosTurno.Confirmado)))
            throw new InvalidOperationException("El perro ya tiene un turno activo para esa fecha.");
        var servicios = dto.ServicioIds.Distinct().ToList();
        if (servicios.Count == 0 || await _context.Servicios.CountAsync(x => servicios.Contains(x.Id)) != servicios.Count)
            throw new InvalidOperationException("Seleccioná al menos un servicio válido.");
        if (dto.TratamientoId is not null && dto.TratamientoId != CatalogosFormularioService.Otro && !await _context.Tratamientos.AnyAsync(x => x.Id == dto.TratamientoId))
            throw new InvalidOperationException("El tratamiento debe registrarlo el veterinario.");
        var tratamientoId = esVeterinario ? await _catalogos.TratamientoAsync(dto.TratamientoId, dto.NuevoTratamiento) : null;
        var turno = new Turno { PerroId = dto.PerroId, VeterinarioId = dto.VeterinarioId, CupoDiarioId = dto.CupoDiarioId,
            EstadoId = EstadosTurno.Solicitado,
            Observaciones = esVeterinario ? dto.Observaciones?.Trim() : null };
        cupo.NumeroCuposDisponibles--;
        _context.Turnos.Add(turno);
        await _context.SaveChangesAsync();
        if (tratamientoId.HasValue)
            _context.TratamientosPorTurno.Add(new TratamientoPorTurno { TurnoId = turno.Id, TratamientoId = tratamientoId.Value });
        foreach (var servicio in servicios) _context.ServiciosPorTurno.Add(new ServicioPorTurno { TurnoId = turno.Id, ServicioId = servicio });
        _auditoria.RegistrarTurno(turno, perro.TutorId, usuarioId, esVeterinario,
            presencial ? AuditoriaAccion.ReservaPresencial : AuditoriaAccion.TurnoSolicitado);
        await _context.SaveChangesAsync(); await transaction.CommitAsync();
        return turno.Id;
    }

    private async Task EditarAsync(TurnoDto dto, string usuarioId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var turno = await BloquearTurnoAsync(dto.Id);
        if (!await PuedeAtenderAsync(turno, usuarioId)) throw new UnauthorizedAccessException();
        if (!EstadosTurno.Activo(turno.EstadoId)) throw new InvalidOperationException("Un turno finalizado no se puede editar.");
        if (turno.PerroId != dto.PerroId || turno.CupoDiarioId != dto.CupoDiarioId || turno.VeterinarioId != dto.VeterinarioId)
            throw new InvalidOperationException("Para cambiar perro, fecha o veterinario, cancelá el turno y generá otro.");
        if (dto.TratamientoId is not null && dto.TratamientoId != CatalogosFormularioService.Otro && !await _context.Tratamientos.AnyAsync(x => x.Id == dto.TratamientoId)) throw new InvalidOperationException("Seleccioná un tratamiento válido.");
        turno.Observaciones = dto.Observaciones?.Trim();
        var tratamientoId = await _catalogos.TratamientoAsync(dto.TratamientoId, dto.NuevoTratamiento);
        var previstos = await _context.TratamientosPorTurno.Where(x => x.TurnoId == turno.Id).ToListAsync();
        _context.TratamientosPorTurno.RemoveRange(previstos.Where(x => x.TratamientoId != tratamientoId));
        if (tratamientoId.HasValue && !previstos.Any(x => x.TratamientoId == tratamientoId))
            _context.TratamientosPorTurno.Add(new TratamientoPorTurno { TurnoId = turno.Id, TratamientoId = tratamientoId.Value });
        var tutorId = await _context.Perros.Where(x => x.Id == turno.PerroId).Select(x => x.TutorId).SingleAsync();
        _auditoria.RegistrarTurno(turno, tutorId, usuarioId, true, AuditoriaAccion.TurnoEditado);
        await _context.SaveChangesAsync(); await transaction.CommitAsync();
    }

    public Task<bool> PuedeAtenderAsync(Turno turno, string usuarioId) =>
        _context.Veterinarios.AnyAsync(veterinario => veterinario.UserId == usuarioId && veterinario.Usuario.Activo
            && (turno.VeterinarioId == null || veterinario.Id == turno.VeterinarioId));

    private async Task<Turno> BloquearTurnoAsync(int id) =>
        await _context.Turnos.FromSqlInterpolated($"SELECT * FROM \"Turnos\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
        ?? throw new InvalidOperationException("No se encontró un turno disponible.");

    public Task CambiarEstadoAsync(int id, int destino, string usuarioId, bool esVeterinario, int? cantidad = null) =>
        CambiarEstadoInternoAsync(id, destino, usuarioId, esVeterinario, cantidad, null);

    public Task CompletarAsync(int id, CompletarTurnoDto atencion, string usuarioId) =>
        CambiarEstadoInternoAsync(id, EstadosTurno.Completado, usuarioId, true, null, atencion);

    private async Task RegistrarAtencionAsync(Turno turno, CompletarTurnoDto datos)
    {
        if (datos.ServicioIdsCompletados is null || datos.Tratamientos is null || datos.Tratamientos.Any(x => x is null))
            throw new InvalidOperationException("Indicá servicios y tratamientos válidos.");
        if (datos.Observaciones?.Length > 2000 || datos.Tratamientos.Any(x => x.Razon?.Length > 1000))
            throw new InvalidOperationException("Revisá la longitud de las observaciones y de los motivos de tratamientos.");
        var servicios = await _context.ServiciosPorTurno.Where(x => x.TurnoId == turno.Id).ToListAsync();
        if (servicios.Count == 0) throw new InvalidOperationException("El turno debe tener al menos un servicio registrado.");
        var realizados = datos.ServicioIdsCompletados.ToHashSet();
        if (realizados.Count != datos.ServicioIdsCompletados.Count || !realizados.IsSubsetOf(servicios.Select(x => x.ServicioId)))
            throw new InvalidOperationException("Indicá servicios distintos que pertenezcan al turno.");
        var ids = datos.Tratamientos.Select(x => x.TratamientoId).ToList();
        if (ids.Count != ids.Distinct().Count() || await _context.Tratamientos.CountAsync(x => ids.Contains(x.Id)) != ids.Count)
            throw new InvalidOperationException("Los tratamientos indicados deben existir y ser distintos.");
        var actuales = await _context.TratamientosPorTurno.Where(x => x.TurnoId == turno.Id).ToListAsync();
        _context.TratamientosPorTurno.RemoveRange(actuales.Where(x => !ids.Contains(x.TratamientoId)));
        foreach (var dato in datos.Tratamientos)
        {
            var tratamiento = actuales.SingleOrDefault(x => x.TratamientoId == dato.TratamientoId);
            if (tratamiento is null)
            {
                tratamiento = new TratamientoPorTurno { TurnoId = turno.Id, TratamientoId = dato.TratamientoId };
                _context.TratamientosPorTurno.Add(tratamiento);
            }
            tratamiento.Razon = string.IsNullOrWhiteSpace(dato.Razon) ? null : dato.Razon.Trim();
        }
        foreach (var servicio in servicios) servicio.Completado = realizados.Contains(servicio.ServicioId);
        turno.Observaciones = string.IsNullOrWhiteSpace(datos.Observaciones) ? null : datos.Observaciones.Trim();
    }

    private async Task CambiarEstadoInternoAsync(int id, int destino, string usuarioId, bool esVeterinario,
        int? cantidad, CompletarTurnoDto? atencion)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        // Leer solo la pertenencia antes de bloquear: todas las operaciones que afectan
        // vetos, conteo o reservas siguen el orden tutor -> turno -> cupo.
        var tutor = await _context.Turnos.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Perro.TutorId, x.Perro.Tutor.UserId }).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("No se encontró un turno disponible.");
        await _vetos.BloquearTutorAsync(tutor.TutorId);
        var turno = await BloquearTurnoAsync(id);
        // Un mismo contexto puede haber leído el turno antes de esperar el bloqueo.
        await _context.Entry(turno).ReloadAsync();
        if (esVeterinario && !await PuedeAtenderAsync(turno, usuarioId)) throw new UnauthorizedAccessException();
        if (!esVeterinario && (tutor.UserId != usuarioId || destino != EstadosTurno.CanceladoTutor))
            throw new InvalidOperationException("No tenés permiso para esta operación.");
        var permitido = esVeterinario
            ? turno.EstadoId == EstadosTurno.Solicitado && (destino == EstadosTurno.Confirmado || destino == EstadosTurno.Rechazado || destino == EstadosTurno.CanceladoVeterinario)
                || turno.EstadoId == EstadosTurno.Confirmado && (destino == EstadosTurno.Completado || destino == EstadosTurno.CanceladoVeterinario)
            : EstadosTurno.Activo(turno.EstadoId);
        if (!permitido) throw new InvalidOperationException("El turno ya cambió de estado o la transición no está permitida.");
        if (destino == EstadosTurno.Completado)
        {
            var fecha = await _context.CuposDiarios.Where(x => x.Id == turno.CupoDiarioId).Select(x => x.FechaCupo).SingleAsync();
            if (fecha > Reloj.Hoy) throw new InvalidOperationException("No se puede completar el turno antes de su fecha.");
            if (atencion is null) throw new InvalidOperationException("Registrá la atención antes de completar el turno.");
            await RegistrarAtencionAsync(turno, atencion);
        }
        if (cantidad is not null && (destino != EstadosTurno.Confirmado || cantidad < 1))
            throw new InvalidOperationException("La cantidad de plazas debe ser mayor que cero y solo se ajusta al confirmar.");
        var liberaReserva = destino is EstadosTurno.Rechazado or EstadosTurno.CanceladoTutor or EstadosTurno.CanceladoVeterinario;
        if (liberaReserva || cantidad is not null && cantidad != turno.CantidadReservada)
        {
            var cupo = await _context.CuposDiarios.FromSqlInterpolated($"SELECT * FROM \"CuposDiarios\" WHERE \"Id\" = {turno.CupoDiarioId} FOR UPDATE").SingleAsync();
            var nuevoDisponible = (long)cupo.NumeroCuposDisponibles + turno.CantidadReservada;
            if (!liberaReserva) nuevoDisponible -= cantidad!.Value;
            if (nuevoDisponible < 0)
                throw new InvalidOperationException("No quedan suficientes plazas para confirmar el turno.");
            if (nuevoDisponible > int.MaxValue)
                throw new InvalidOperationException("La cantidad de plazas supera el máximo permitido.");
            cupo.NumeroCuposDisponibles = (int)nuevoDisponible;
        }
        if (cantidad is not null) turno.CantidadReservada = cantidad.Value;
        var estadoAnterior = turno.EstadoId;
        turno.EstadoId = destino;
        _auditoria.RegistrarTurno(turno, tutor.TutorId, usuarioId, esVeterinario, AuditoriaAccion.EstadoTurnoCambiado, estadoAnterior);
        await _context.SaveChangesAsync();
        if (!esVeterinario && destino == EstadosTurno.CanceladoTutor)
            await _vetos.EvaluarCancelacionAsync(tutor.TutorId, usuarioId);
        await transaction.CommitAsync();
    }
}
