using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;
namespace VetWeb.Controllers.Api;
[ApiController, Authorize(Roles = "Veterinario,Tutor")]
public abstract class CatalogoApiController<T>(CatalogoService<T> catalogo) : ControllerBase where T : class, new()
{
    [HttpGet]
    public Task<List<CatalogoDto>> Listar([Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 100) =>
        catalogo.Consultar().Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatalogoDto>> Obtener(int id)
    { var dato = await catalogo.Consultar().SingleOrDefaultAsync(x => x.Id == id); return dato is null ? NotFound() : Ok(dato); }
    [HttpPost, Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Crear(CatalogoDto dto)
    {
        if (dto.Id != 0) return BadRequest();
        var id = await catalogo.GuardarAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, await catalogo.Consultar().SingleAsync(x => x.Id == id));
    }
    [HttpPut("{id:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Modificar(int id, CatalogoDto dto)
    {
        if (dto.Id != 0 && dto.Id != id) return BadRequest();
        dto.Id = id; return await catalogo.GuardarAsync(dto) is null ? NotFound() : NoContent();
    }
    [HttpDelete("{id:int}"), Authorize(Roles = "Veterinario")]
    public async Task<IActionResult> Eliminar(int id) => await catalogo.EliminarAsync(id) ? NoContent() : NotFound();
}
[Route("api/servicios")]
public class ServiciosApiController(CatalogoService<Servicio> catalogo) : CatalogoApiController<Servicio>(catalogo);
[Route("api/grupos-etarios")]
public class GruposEtariosApiController(CatalogoService<GrupoEtario> catalogo) : CatalogoApiController<GrupoEtario>(catalogo);
[Route("api/tamanios")]
public class TamaniosApiController(CatalogoService<Tamanio> catalogo) : CatalogoApiController<Tamanio>(catalogo);
[Route("api/patologias"), Authorize(Roles = "Veterinario")]
public class PatologiasApiController(CatalogoService<Patologia> catalogo) : CatalogoApiController<Patologia>(catalogo);
[Route("api/tratamientos"), Authorize(Roles = "Veterinario")]
public class TratamientosApiController(CatalogoService<Tratamiento> catalogo) : CatalogoApiController<Tratamiento>(catalogo);
[ApiController, Route("api/estados"), Authorize(Roles = "Veterinario,Tutor")]
public class EstadosApiController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<CatalogoDto>> Listar() => db.Estados.OrderBy(x => x.Id).Select(x => new CatalogoDto { Id = x.Id, Descripcion = x.Descripcion }).ToListAsync();
    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatalogoDto>> Obtener(int id)
    {
        var dato = await db.Estados.Where(x => x.Id == id).Select(x => new CatalogoDto { Id = x.Id, Descripcion = x.Descripcion }).SingleOrDefaultAsync();
        return dato is null ? NotFound() : Ok(dato);
    }
}
