using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Services;

namespace VetWeb.Controllers.Api;

[ApiController]
[Route("api/cupos")]
[Authorize(Roles = "Veterinario,Tutor")]
public class CuposController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<CupoDiarioDto>> Listar()
    {
        var cupos = db.CuposDiarios.AsNoTracking().Where(x => x.FechaCupo >= Reloj.Hoy);
        if (!User.IsInRole("Veterinario"))
            cupos = cupos.Where(x => x.NumeroCuposDisponibles > 0);
        return cupos.OrderBy(x => x.FechaCupo)
            .Select(x => new CupoDiarioDto
            {
                Id = x.Id, FechaCupo = x.FechaCupo, NumeroCuposDisponibles = x.NumeroCuposDisponibles
            }).ToListAsync();
    }
}
