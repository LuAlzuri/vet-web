using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using VetWeb.Dtos;
using VetWeb.Services;

namespace VetWeb.Controllers;

[Authorize(Roles = RolSesion.Administrador)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class InformesController(InformesService informes, InformesPdfService pdf) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Listado(InformePaginaDto pagina, CancellationToken ct)
    {
        if (Request.Query.Count == 0)
        {
            var hoy = DateOnly.FromDateTime(Reloj.Hoy);
            pagina.Periodo = new PeriodoInformeDto { Desde = new DateOnly(hoy.Year, hoy.Month, 1), Hasta = hoy };
            ModelState.Clear();
        }
        if (!Enum.IsDefined(pagina.Tipo)) ModelState.AddModelError(nameof(pagina.Tipo), "Seleccioná un informe válido.");
        if (ModelState.IsValid)
            pagina.Informe = await informes.ConsultarAsync(pagina.Tipo, pagina.Periodo, ct);
        return View(pagina);
    }

    [HttpGet]
    public async Task<IActionResult> Pdf(TipoInforme tipo, PeriodoInformeDto periodo, CancellationToken ct, bool abrir = false)
    {
        if (!ModelState.IsValid || !Enum.IsDefined(tipo)) return BadRequest(ModelState);
        var contenido = pdf.Generar(await informes.ConsultarAsync(tipo, periodo, ct));
        var nombre = InformesPdfService.Archivo(tipo);
        if (!abrir) return File(contenido, "application/pdf", nombre);

        var disposicion = new ContentDispositionHeaderValue("inline");
        disposicion.SetHttpFileName(nombre);
        Response.GetTypedHeaders().ContentDisposition = disposicion;
        return File(contenido, "application/pdf");
    }
}
