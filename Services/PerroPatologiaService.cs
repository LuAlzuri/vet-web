using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
namespace VetWeb.Services;
public class PerroPatologiaService(ApplicationDbContext db, CatalogosFormularioService catalogos)
{
    public async Task<int?> GuardarAsync(PerroPatologiaDto dto)
    {
        ValidacionDto.Comprobar(dto);
        if (!await db.Perros.AnyAsync(x => x.Id == dto.PerroId)) throw new InvalidOperationException("Seleccioná un perro activo.");
        if (dto.PatologiaId != CatalogosFormularioService.Otro && !await db.Patologias.AnyAsync(x => x.Id == dto.PatologiaId))
            throw new InvalidOperationException("Seleccioná una patología válida.");
        if (dto.EsEdicion && dto.PatologiaId == CatalogosFormularioService.Otro)
            throw new InvalidOperationException("Para cambiar de patología, agregá una asociación nueva.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var patologiaId = await catalogos.PatologiaAsync(dto.PatologiaId, dto.NuevaPatologia);
        var relacion = await db.PerrosPatologias.SingleOrDefaultAsync(x => x.PerroId == dto.PerroId && x.PatologiaId == patologiaId);
        if (!dto.EsEdicion && relacion is not null) throw new InvalidOperationException("El perro ya tiene esa patología.");
        if (dto.EsEdicion && relacion is null) return null;
        if (relacion is null) { relacion = new PerroPatologia { PerroId = dto.PerroId, PatologiaId = patologiaId }; db.PerrosPatologias.Add(relacion); }
        relacion.Controlado = dto.Controlado; relacion.Observacion = dto.Observacion?.Trim();
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return patologiaId;
    }
    public async Task<bool> EliminarAsync(int perroId, int patologiaId)
    {
        var relacion = await db.PerrosPatologias.SingleOrDefaultAsync(x => x.PerroId == perroId && x.PatologiaId == patologiaId);
        if (relacion is null) return false;
        db.PerrosPatologias.Remove(relacion); await db.SaveChangesAsync(); return true;
    }
}
