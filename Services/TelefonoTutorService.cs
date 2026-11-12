using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
namespace VetWeb.Services;
public class TelefonoTutorService(ApplicationDbContext db)
{
    public async Task<int?> GuardarAsync(TelefonoTutorDto dto, int? tutorOrigenId = null)
    {
        ValidacionDto.Comprobar(dto);
        if (!await db.Tutores.AnyAsync(x => x.Id == dto.TutorId && x.Usuario.Activo)) throw new InvalidOperationException("Seleccioná un tutor activo.");
        var telefono = dto.Id == 0 ? new TelefonoTutor() : await db.TelefonosTutores
            .SingleOrDefaultAsync(x => x.Id == dto.Id && (tutorOrigenId == null || x.TutorId == tutorOrigenId));
        if (telefono is null) return null;
        telefono.TutorId = dto.TutorId; telefono.NombrePropietario = dto.NombrePropietario.Trim(); telefono.Numero = dto.Numero.Trim();
        if (dto.Id == 0) db.TelefonosTutores.Add(telefono);
        await db.SaveChangesAsync(); return telefono.Id;
    }
    public async Task<bool> EliminarAsync(int id, int? tutorId = null)
    {
        var telefono = await db.TelefonosTutores.SingleOrDefaultAsync(x => x.Id == id && (tutorId == null || x.TutorId == tutorId));
        if (telefono is null) return false;
        db.TelefonosTutores.Remove(telefono); await db.SaveChangesAsync(); return true;
    }
}
