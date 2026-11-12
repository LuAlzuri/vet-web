using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VetWeb.Dtos;
using VetWeb.Services;

namespace VetWeb.Controllers.Api;

[ApiController]
[Route("api/reportes")]
[Authorize(Roles = RolSesion.Administrador)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class InformesApiController(InformesService informes, InformesPdfService pdf) : ControllerBase
{
    [HttpGet("frecuencia-solicitudes/datos")]
    public Task<InformeDto> FrecuenciaDatos([FromQuery] PeriodoInformeDto periodo, CancellationToken ct) =>
        informes.ConsultarAsync(TipoInforme.FrecuenciaSolicitudes, periodo, ct);

    [HttpGet("servicios-completos/datos")]
    public Task<InformeDto> CompletosDatos([FromQuery] PeriodoInformeDto periodo, CancellationToken ct) =>
        informes.ConsultarAsync(TipoInforme.ServiciosCompletos, periodo, ct);

    [HttpGet("servicios-incompletos/datos")]
    public Task<InformeDto> IncompletosDatos([FromQuery] PeriodoInformeDto periodo, CancellationToken ct) =>
        informes.ConsultarAsync(TipoInforme.ServiciosIncompletos, periodo, ct);

    [HttpGet("frecuencia-solicitudes")]
    public Task<IActionResult> FrecuenciaPdf([FromQuery] PeriodoInformeDto periodo, CancellationToken ct) =>
        Descargar(TipoInforme.FrecuenciaSolicitudes, periodo, ct);

    [HttpGet("servicios-completos")]
    public Task<IActionResult> CompletosPdf([FromQuery] PeriodoInformeDto periodo, CancellationToken ct) =>
        Descargar(TipoInforme.ServiciosCompletos, periodo, ct);

    [HttpGet("servicios-incompletos")]
    public Task<IActionResult> IncompletosPdf([FromQuery] PeriodoInformeDto periodo, CancellationToken ct) =>
        Descargar(TipoInforme.ServiciosIncompletos, periodo, ct);

    private async Task<IActionResult> Descargar(TipoInforme tipo, PeriodoInformeDto periodo, CancellationToken ct) =>
        File(pdf.Generar(await informes.ConsultarAsync(tipo, periodo, ct)), "application/pdf", InformesPdfService.Archivo(tipo));
}
