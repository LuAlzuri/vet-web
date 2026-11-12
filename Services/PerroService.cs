using System.Data;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
namespace VetWeb.Services;
public class PerroService(ApplicationDbContext db, ImagenPerroService imagenes)
{
    public IQueryable<Perro> Visibles(string usuarioId, bool veterinario) =>
        db.Perros.AsNoTracking().Where(x => veterinario || x.Tutor.UserId == usuarioId);

    public async Task<int?> GuardarAsync(PerroDto dto, ImagenPerroService.FotoValidada? foto = null)
    {
        ValidacionDto.Comprobar(dto);
        if (dto.FechaNacimiento == DateTime.MinValue || dto.FechaNacimiento > Reloj.Hoy)
            throw new InvalidOperationException("La fecha debe ser válida y no puede ser futura.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await db.Tutores.AnyAsync(x => x.Id == dto.TutorId && x.Usuario.Activo)) throw new InvalidOperationException("Seleccioná un tutor activo.");
        if (!await db.GruposEtarios.AnyAsync(x => x.Id == dto.GrupoEtarioId)) throw new InvalidOperationException("Seleccioná un grupo etario válido.");
        if (!await db.Tamanios.AnyAsync(x => x.Id == dto.TamanioId)) throw new InvalidOperationException("Seleccioná un tamaño válido.");
        var perro = dto.Id == 0 ? new Perro() : await db.Perros.Include(x => x.Imagen).SingleOrDefaultAsync(x => x.Id == dto.Id);
        if (perro is null) return null;
        if (dto.Id != 0 && perro.TutorId != dto.TutorId && await db.Turnos.IgnoreQueryFilters().AnyAsync(x => x.PerroId == dto.Id))
            throw new InvalidOperationException("No se puede cambiar el tutor de un perro con historial de turnos.");
        perro.Nombre = dto.Nombre.Trim(); perro.FechaNacimiento = dto.FechaNacimiento!.Value.Date;
        perro.TutorId = dto.TutorId; perro.GrupoEtarioId = dto.GrupoEtarioId; perro.TamanioId = dto.TamanioId;
        perro.Reactivo = dto.Reactivo; perro.CalendarioVacunasCompleto = dto.CalendarioVacunasCompleto;
        if (foto is null && dto.Foto is not null) foto = await imagenes.ValidarAsync(dto.Foto);
        var anterior = perro.Imagen?.NombreArchivo;
        string? nueva = null;
        try
        {
            if (foto is not null) nueva = await imagenes.AsignarAsync(perro, foto);
            if (dto.Id == 0) db.Perros.Add(perro);
            await db.SaveChangesAsync(); await transaction.CommitAsync();
        }
        catch { imagenes.Eliminar(nueva); throw; }
        if (nueva is not null) imagenes.Eliminar(anterior);
        return perro.Id;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var perro = await db.Perros.SingleOrDefaultAsync(x => x.Id == id);
        if (perro is null) return false;
        if (await db.Turnos.AnyAsync(x => x.PerroId == id && (x.EstadoId == EstadosTurno.Solicitado || x.EstadoId == EstadosTurno.Confirmado)))
            throw new InvalidOperationException("Cancelá o completá los turnos pendientes antes de dar de baja al perro.");
        perro.Eliminado = true; await db.SaveChangesAsync(); await transaction.CommitAsync();
        return true;
    }
}
