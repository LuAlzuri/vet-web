using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Services;

public class VetoService(ApplicationDbContext db)
{
    public IQueryable<Veto> Vigentes()
    {
        var ahora = DateTime.UtcNow;
        return db.Vetos.Where(x => x.FechaInicio <= ahora && (x.FechaFin == null || x.FechaFin > ahora));
    }

    public Task<List<VetoDto>> ConsultarAsync(int tutorId)
    {
        var ahora = DateTime.UtcNow;
        return db.Vetos.AsNoTracking().Where(x => x.TutorId == tutorId).OrderByDescending(x => x.FechaInicio)
            .Select(x => new VetoDto(x.Id, x.TutorId, x.VeterinarioId, x.FechaInicio, x.FechaFin,
                x.Motivo, x.MotivoFinVeto, x.FechaInicio <= ahora && (x.FechaFin == null || x.FechaFin > ahora)))
            .ToListAsync();
    }

    // Las altas, levantamientos y reservas se coordinan sobre el mismo tutor.
    internal async Task BloquearTutorAsync(int tutorId)
    {
        var tutor = await db.Tutores.FromSqlInterpolated($"SELECT * FROM \"Tutores\" WHERE \"Id\" = {tutorId} FOR UPDATE")
            .Where(x => x.Usuario.Activo).Select(x => x.Id).SingleOrDefaultAsync();
        if (tutor == 0) throw new InvalidOperationException("El tutor no está disponible.");
    }

    public async Task<int> AplicarAsync(int tutorId, string usuarioId, string motivo)
    {
        var texto = ValidarMotivo(motivo);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await BloquearTutorAsync(tutorId);
        var veterinarioId = await ObtenerVeterinarioIdAsync(usuarioId);
        if (await Vigentes().AnyAsync(x => x.TutorId == tutorId))
            throw new InvalidOperationException("El tutor ya tiene un veto vigente.");
        var veto = new Veto { TutorId = tutorId, VeterinarioId = veterinarioId, FechaInicio = DateTime.UtcNow, Motivo = texto };
        db.Vetos.Add(veto);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return veto.Id;
    }

    public async Task LevantarAsync(int tutorId, int vetoId, string usuarioId, string motivo)
    {
        var texto = ValidarMotivo(motivo);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await BloquearTutorAsync(tutorId);
        await ObtenerVeterinarioIdAsync(usuarioId);
        var veto = await Vigentes().SingleOrDefaultAsync(x => x.Id == vetoId && x.TutorId == tutorId)
            ?? throw new InvalidOperationException("El veto indicado no está vigente para este tutor.");
        veto.FechaFin = DateTime.UtcNow;
        veto.MotivoFinVeto = texto;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    internal async Task<int> ObtenerVeterinarioIdAsync(string usuarioId) =>
        await db.Veterinarios.Where(x => x.UserId == usuarioId && x.Usuario.Activo)
            .Select(x => (int?)x.Id).SingleOrDefaultAsync() ?? throw new UnauthorizedAccessException();

    private static string ValidarMotivo(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length > 1000)
            throw new InvalidOperationException("Ingresá un motivo de hasta 1000 caracteres.");
        return motivo.Trim();
    }
}
