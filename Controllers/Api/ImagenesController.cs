using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Controllers.Api;

[ApiController]
[Route("api/perros/{perroId:int}/imagen")]
[Authorize(Roles = "Veterinario,Tutor")]
public class ImagenesController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> usuarios,
    ImagenPerroService imagenes) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Obtener(int perroId)
    {
        var perro = await db.Perros.AsNoTracking().Include(x => x.Imagen).Include(x => x.Tutor)
            .SingleOrDefaultAsync(x => x.Id == perroId);
        if (perro is null) return NotFound();
        if (!User.IsInRole("Veterinario") && perro.Tutor.UserId != usuarios.GetUserId(User)) return Forbid();
        if (perro.Imagen is null) return NotFound();

        var ruta = imagenes.Ruta(perro.Imagen.NombreArchivo);
        if (!System.IO.File.Exists(ruta)) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(ruta, perro.Imagen.TipoContenido);
    }

    [HttpPost]
    [Authorize(Roles = "Veterinario")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024)]
    public async Task<ActionResult<ImagenDto>> Cargar(int perroId, [FromForm] CargarImagenDto dto)
    {
        var foto = await imagenes.ValidarAsync(dto.Archivo);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var perro = await db.Perros.Include(x => x.Imagen).SingleOrDefaultAsync(x => x.Id == perroId);
        if (perro is null) return NotFound();
        var anterior = perro.Imagen?.NombreArchivo;
        var nueva = await imagenes.AsignarAsync(perro, foto);
        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            imagenes.Eliminar(nueva);
            throw;
        }
        imagenes.Eliminar(anterior);
        return Ok(new ImagenDto(perroId, perro.Imagen!.TipoContenido, perro.Imagen.TamanoBytes));
    }

    [HttpDelete]
    [Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int perroId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var imagen = await db.Imagenes.SingleOrDefaultAsync(x => x.PerroId == perroId);
        if (imagen is null) return NotFound();
        db.Imagenes.Remove(imagen);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        imagenes.Eliminar(imagen.NombreArchivo);
        return NoContent();
    }
}
