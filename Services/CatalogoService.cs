using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
namespace VetWeb.Services;
// T lo elige el controlador, nunca un nombre de entidad recibido del cliente.
public class CatalogoService<T>(ApplicationDbContext db) where T : class, new()
{
    public IQueryable<CatalogoDto> Consultar() => db.Set<T>().AsNoTracking().OrderBy(x => EF.Property<string>(x, "Descripcion"))
        .ThenBy(x => EF.Property<int>(x, "Id")).Select(x => new CatalogoDto { Id = EF.Property<int>(x, "Id"), Descripcion = EF.Property<string>(x, "Descripcion") });
    public async Task<int?> GuardarAsync(CatalogoDto dto)
    {
        dto.Descripcion = string.Join(" ", (dto.Descripcion ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        ValidacionDto.Comprobar(dto);
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Misma clave que las altas contextuales de patologías y tratamientos.
        var clave = typeof(T).Name + ":" + dto.Descripcion.ToLowerInvariant();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({clave}))");
        if (await db.Set<T>().AnyAsync(x => EF.Property<int>(x, "Id") != dto.Id
            && EF.Property<string>(x, "Descripcion").Trim().ToLower() == dto.Descripcion.ToLower()))
            throw new InvalidOperationException("Ya existe esa descripción.");
        var entidad = dto.Id == 0 ? new T() : await db.Set<T>().FindAsync(dto.Id);
        if (entidad is null) return null;
        db.Entry(entidad).Property("Descripcion").CurrentValue = dto.Descripcion;
        if (dto.Id == 0) db.Set<T>().Add(entidad);
        await db.SaveChangesAsync(); await transaction.CommitAsync();
        return (int)db.Entry(entidad).Property("Id").CurrentValue!;
    }
    public async Task<bool> EliminarAsync(int id)
    {
        var entidad = await db.Set<T>().FindAsync(id);
        if (entidad is null) return false;
        db.Remove(entidad); await db.SaveChangesAsync(); return true;
    }
}
