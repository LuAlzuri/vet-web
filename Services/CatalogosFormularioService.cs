using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Models;

namespace VetWeb.Services;

public class CatalogosFormularioService
{
    public const int Otro = -1;
    private readonly ApplicationDbContext _context;
    public CatalogosFormularioService(ApplicationDbContext context) => _context = context;

    private async Task<string> PrepararAsync(string catalogo, string? descripcion)
    {
        var texto = string.Join(" ", (descripcion ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (texto.Length == 0 || texto.Length > 100)
            throw new InvalidOperationException("La nueva opción debe tener entre 1 y 100 caracteres.");
        if (_context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La opción nueva debe guardarse junto con su formulario.");
        // Serializar altas de la misma descripción para evitar duplicados simultáneos.
        var clave = catalogo + ":" + texto.ToLowerInvariant();
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({clave}))");
        return texto;
    }

    public async Task<int> PatologiaAsync(int id, string? descripcion)
    {
        if (id != Otro) return id;
        var texto = await PrepararAsync("Patologia", descripcion);
        var normalizado = texto.ToLowerInvariant();
        var existente = await _context.Patologias.FirstOrDefaultAsync(x => x.Descripcion.Trim().ToLower() == normalizado);
        if (existente is not null) return existente.Id;
        var nuevo = new Patologia { Descripcion = texto };
        _context.Patologias.Add(nuevo);
        await _context.SaveChangesAsync();
        return nuevo.Id;
    }

    public async Task<int?> TratamientoAsync(int? id, string? descripcion)
    {
        if (id != Otro) return id;
        var texto = await PrepararAsync("Tratamiento", descripcion);
        var normalizado = texto.ToLowerInvariant();
        var existente = await _context.Tratamientos.FirstOrDefaultAsync(x => x.Descripcion.Trim().ToLower() == normalizado);
        if (existente is not null) return existente.Id;
        var nuevo = new Tratamiento { Descripcion = texto };
        _context.Tratamientos.Add(nuevo);
        await _context.SaveChangesAsync();
        return nuevo.Id;
    }
}
