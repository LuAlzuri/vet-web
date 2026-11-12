using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VetWeb.Dtos;

namespace VetWeb.Services;

public class InformesPdfService
{
    private const string Azul = "#173E52";
    private const string Borde = "#D7E2E8";
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    public static string Archivo(TipoInforme tipo) => tipo switch
    {
        TipoInforme.FrecuenciaSolicitudes => "frecuencia-solicitudes.pdf",
        TipoInforme.ServiciosCompletos => "servicios-completos.pdf",
        TipoInforme.ServiciosIncompletos => "servicios-incompletos.pdf",
        _ => throw new InvalidOperationException("Seleccioná un informe válido.")
    };

    public byte[] Generar(InformeDto informe) => Document.Create(documento => documento.Page(pagina =>
    {
        pagina.Size(PageSizes.A4);
        pagina.Margin(36);
        pagina.DefaultTextStyle(x => x.FontSize(10));
        pagina.Header().BorderBottom(1).BorderColor(Borde).PaddingBottom(10).Column(c =>
        {
            c.Spacing(4);
            c.Item().Text("VETERINARIA EL REFUGIO · VETWEB").FontSize(9).FontColor(Azul);
            c.Item().Text(InformesService.Titulo(informe.Tipo)).FontSize(21).SemiBold().FontColor(Azul);
            c.Item().Text($"Período: {Fecha(informe.Desde)} al {Fecha(informe.Hasta)} · Hora argentina");
        });
        pagina.Content().PaddingVertical(14).Column(c =>
        {
            c.Spacing(12);
            c.Item().Text(InformesService.Criterio(informe.Tipo));
            c.Item().Text(informe.AuditoriaDesdeUtc is DateTime inicio
                ? $"Primer evento de auditoría registrado: {Reloj.EnArgentina(inicio):dd/MM/yyyy HH:mm:ss} (Argentina). No se reconstruyen eventos anteriores."
                : "Todavía no hay eventos de auditoría registrados. No se reconstruyen eventos anteriores.").FontSize(9).FontColor(Colors.Grey.Darken2);
            c.Item().Background("#E9F3F5").Padding(12).Text($"Total: {informe.Total.ToString("N0", Cultura)}").FontSize(18).SemiBold();
            if (informe.Tipo == TipoInforme.FrecuenciaSolicitudes)
                c.Item().Element(x => Tabla(x, ["Fecha de solicitud", "Solicitudes"],
                    informe.Solicitudes.Select(s => new[] { Fecha(s.Fecha), s.Cantidad.ToString(Cultura) })));
            else
            {
                c.Item().Element(x => Tabla(x, ["Turno / Finalización", "Perro / Tutor", "Servicios realizados", "Observaciones"],
                    informe.Atenciones.Select(a => new[] { $"#{a.TurnoId}\n{Reloj.EnArgentina(a.FinalizacionUtc):dd/MM/yyyy HH:mm}",
                        $"{a.Perro}\n{a.Tutor}", $"{a.Completados} de {a.Servicios}",
                        string.IsNullOrWhiteSpace(a.Observaciones) ? "Sin observaciones" : a.Observaciones })));
                if (informe.Tipo == TipoInforme.ServiciosIncompletos)
                {
                    c.Item().Text("Patologías actuales asociadas").FontSize(14).SemiBold().FontColor(Azul);
                    c.Item().Text("Cada patología se cuenta una vez por atención, aunque haya varios servicios pendientes.").FontSize(9);
                    c.Item().Element(x => Tabla(x, ["Patología", "Atenciones"],
                        informe.Patologias.Select(p => new[] { p.Descripcion, p.Cantidad.ToString(Cultura) })));
                }
            }
        });
        pagina.Footer().BorderTop(1).BorderColor(Borde).PaddingTop(8).Row(fila =>
        {
            fila.RelativeItem().Text("Informe de gestión · El Refugio").FontSize(9);
            fila.AutoItem().Text(t => { t.Span("Página "); t.CurrentPageNumber(); t.Span(" de "); t.TotalPages(); });
        });
    })).GeneratePdf();

    private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", Cultura);
    private static void Tabla(IContainer contenedor, string[] columnas, IEnumerable<string[]> filas)
    {
        var datos = filas.ToList();
        if (datos.Count == 0) { contenedor.Text("No hay datos para el período seleccionado."); return; }
        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(c => { foreach (var _ in columnas) c.RelativeColumn(); });
            tabla.Header(h => { foreach (var columna in columnas) h.Cell().Background(Azul).Padding(6).Text(columna).FontColor(Colors.White).SemiBold(); });
            foreach (var fila in datos)
                foreach (var valor in fila)
                    tabla.Cell().BorderBottom(1).BorderColor(Borde).Padding(6).Text(valor);
        });
    }
}
