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
    public TurnoService(ApplicationDbContext context, CatalogosFormularioService catalogos) { _context = context; _catalogos = catalogos; }

    public async Task GuardarAsync(TurnoDto dto, string usuarioId, bool esVeterinario)
    {
        if (!esVeterinario && (dto.TratamientoId is not null || !string.IsNullOrWhiteSpace(dto.NuevoTratamiento)))
            throw new InvalidOperationException("El tratamiento debe registrarlo el veterinario.");
        if (dto.Id != 0)
        {
            if (!esVeterinario) throw new InvalidOperationException("Solo el veterinario puede editar un turno solicitado.");
            await EditarAsync(dto, usuarioId); return;
        }
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var perro = await _context.Perros.Include(x => x.Tutor).ThenInclude(x => x.Usuario).SingleOrDefaultAsync(x => x.Id == dto.PerroId);
        if (perro is null || !perro.Tutor.Usuario.Activo) throw new InvalidOperationException("El perro o su tutor no están disponibles.");
        if (!esVeterinario && perro.Tutor.UserId != usuarioId) throw new InvalidOperationException("Solo podés solicitar turnos para tus perros.");
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
            EstadoId = EstadosTurno.Solicitado, TratamientoId = tratamientoId,
            Observaciones = esVeterinario ? dto.Observaciones?.Trim() : null };
        cupo.NumeroCuposDisponibles--;
        _context.Turnos.Add(turno);
        await _context.SaveChangesAsync();
        foreach (var servicio in servicios) _context.ServiciosPorTurno.Add(new ServicioPorTurno { TurnoId = turno.Id, ServicioId = servicio });
        await _context.SaveChangesAsync(); await transaction.CommitAsync();
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
        turno.Observaciones = dto.Observaciones?.Trim(); turno.TratamientoId = await _catalogos.TratamientoAsync(dto.TratamientoId, dto.NuevoTratamiento);
        await _context.SaveChangesAsync(); await transaction.CommitAsync();
    }

    public Task<bool> PuedeAtenderAsync(Turno turno, string usuarioId) =>
        _context.Veterinarios.AnyAsync(veterinario => veterinario.UserId == usuarioId && veterinario.Usuario.Activo
            && (turno.VeterinarioId == null || veterinario.Id == turno.VeterinarioId));

    private async Task<Turno> BloquearTurnoAsync(int id) =>
        await _context.Turnos.FromSqlInterpolated($"SELECT * FROM \"Turnos\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
        ?? throw new InvalidOperationException("No se encontró un turno disponible.");

    public async Task CambiarEstadoAsync(int id, int destino, string usuarioId, bool esVeterinario)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        var turno = await BloquearTurnoAsync(id);
        if (esVeterinario && !await PuedeAtenderAsync(turno, usuarioId)) throw new UnauthorizedAccessException();
        var tutorUsuarioId = await _context.Perros.Where(x => x.Id == turno.PerroId).Select(x => x.Tutor.UserId).SingleAsync();
        if (!esVeterinario && (tutorUsuarioId != usuarioId || destino != EstadosTurno.CanceladoTutor))
            throw new InvalidOperationException("No tenés permiso para esta operación.");
        var permitido = esVeterinario
            ? turno.EstadoId == EstadosTurno.Solicitado && (destino == EstadosTurno.Confirmado || destino == EstadosTurno.Rechazado || destino == EstadosTurno.CanceladoVeterinario)
                || turno.EstadoId == EstadosTurno.Confirmado && (destino == EstadosTurno.Completado || destino == EstadosTurno.CanceladoVeterinario)
            : EstadosTurno.Activo(turno.EstadoId);
        if (!permitido) throw new InvalidOperationException("El turno ya cambió de estado o la transición no está permitida.");
        if (destino == EstadosTurno.Rechazado || destino == EstadosTurno.CanceladoTutor || destino == EstadosTurno.CanceladoVeterinario)
        {
            var cupo = await _context.CuposDiarios.FromSqlInterpolated($"SELECT * FROM \"CuposDiarios\" WHERE \"Id\" = {turno.CupoDiarioId} FOR UPDATE").SingleAsync();
            cupo.NumeroCuposDisponibles++;
        }
        turno.EstadoId = destino;
        await _context.SaveChangesAsync(); await transaction.CommitAsync();
    }
}
