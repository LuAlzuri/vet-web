using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
namespace VetWeb.Services;
public class ServicioTurnoService(ApplicationDbContext db, TurnoService turnos)
{
    private async Task BloquearAsync(int turnoId, string usuarioId)
    {
        var turno = await db.Turnos.FromSqlInterpolated($"SELECT * FROM \"Turnos\" WHERE \"Id\" = {turnoId} FOR UPDATE").SingleOrDefaultAsync();
        if (turno is not null)
        {
            await db.Entry(turno).ReloadAsync();
            if (!await turnos.PuedeAtenderAsync(turno, usuarioId)) throw new UnauthorizedAccessException();
        }
        if (turno is null || !EstadosTurno.Activo(turno.EstadoId)) throw new InvalidOperationException("Solo se pueden modificar los servicios de un turno activo.");
    }
    public async Task<bool> GuardarAsync(ServicioPorTurnoDto dto, string usuarioId)
    {
        ValidacionDto.Comprobar(dto);
        if (!await db.Servicios.AnyAsync(x => x.Id == dto.ServicioId)) throw new InvalidOperationException("Seleccioná un servicio válido.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        await BloquearAsync(dto.TurnoId, usuarioId);
        var relacion = await db.ServiciosPorTurno.SingleOrDefaultAsync(x => x.TurnoId == dto.TurnoId && x.ServicioId == dto.ServicioId);
        if (!dto.EsEdicion && relacion is not null) throw new InvalidOperationException("El servicio ya está asociado al turno.");
        if (dto.EsEdicion && relacion is null) return false;
        if (relacion is null) { relacion = new ServicioPorTurno { TurnoId = dto.TurnoId, ServicioId = dto.ServicioId }; db.ServiciosPorTurno.Add(relacion); }
        relacion.Completado = dto.Completado; await db.SaveChangesAsync(); await transaction.CommitAsync(); return true;
    }
    public async Task<bool> EliminarAsync(int turnoId, int servicioId, string usuarioId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await BloquearAsync(turnoId, usuarioId);
        var relacion = await db.ServiciosPorTurno.SingleOrDefaultAsync(x => x.TurnoId == turnoId && x.ServicioId == servicioId);
        if (relacion is null) return false;
        if (relacion.Completado) throw new InvalidOperationException("No se puede eliminar un servicio ya realizado.");
        if (await db.ServiciosPorTurno.CountAsync(x => x.TurnoId == turnoId) <= 1) throw new InvalidOperationException("El turno debe conservar al menos un servicio.");
        db.ServiciosPorTurno.Remove(relacion); await db.SaveChangesAsync(); await transaction.CommitAsync(); return true;
    }
}
