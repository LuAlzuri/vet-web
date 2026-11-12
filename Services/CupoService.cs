using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
namespace VetWeb.Services;
public class CupoService(ApplicationDbContext db)
{
    public async Task<int?> GuardarAsync(CupoDiarioDto dto)
    {
        ValidacionDto.Comprobar(dto);
        if (dto.FechaCupo!.Value.Date < Reloj.Hoy) throw new InvalidOperationException("No se pueden publicar cupos en fechas pasadas.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var cupo = dto.Id == 0 ? new CupoDiario() : await db.CuposDiarios.FromSqlInterpolated($"SELECT * FROM \"CuposDiarios\" WHERE \"Id\" = {dto.Id} FOR UPDATE").SingleOrDefaultAsync();
        if (cupo is null) return null;
        if (dto.Id != 0 && cupo.FechaCupo != dto.FechaCupo.Value.Date && await db.Turnos.IgnoreQueryFilters().AnyAsync(x => x.CupoDiarioId == dto.Id))
            throw new InvalidOperationException("No se puede cambiar la fecha de un cupo con turnos registrados.");
        cupo.FechaCupo = dto.FechaCupo.Value.Date; cupo.NumeroCuposDisponibles = dto.NumeroCuposDisponibles;
        if (dto.Id == 0) db.CuposDiarios.Add(cupo);
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return cupo.Id;
    }
    public async Task<bool> EliminarAsync(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var cupo = await db.CuposDiarios.FromSqlInterpolated($"SELECT * FROM \"CuposDiarios\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync();
        if (cupo is null) return false;
        if (await db.Turnos.IgnoreQueryFilters().AnyAsync(x => x.CupoDiarioId == id))
            throw new InvalidOperationException("No se puede eliminar un cupo con turnos registrados.");
        db.CuposDiarios.Remove(cupo); await db.SaveChangesAsync(); await transaction.CommitAsync(); return true;
    }
}
