using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public enum TipoInforme { FrecuenciaSolicitudes, ServiciosCompletos, ServiciosIncompletos }

public class PeriodoInformeDto : IValidatableObject
{
    [Required(ErrorMessage = "Indicá la fecha inicial.")]
    [Display(Name = "Desde")]
    [DataType(DataType.Date)]
    public DateOnly? Desde { get; set; }

    [Required(ErrorMessage = "Indicá la fecha final.")]
    [Display(Name = "Hasta")]
    [DataType(DataType.Date)]
    public DateOnly? Hasta { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Desde.HasValue && Hasta.HasValue && Desde > Hasta)
            yield return new ValidationResult("La fecha inicial debe ser menor o igual a la final.", [nameof(Hasta)]);
        if (Hasta == DateOnly.MaxValue)
            yield return new ValidationResult("La fecha final debe ser anterior al 31/12/9999.", [nameof(Hasta)]);
    }
}

public sealed record SolicitudesPorDiaDto(DateOnly Fecha, int Cantidad);
public sealed record AtencionInformeDto(int TurnoId, int PerroId, string Perro, string Tutor,
    DateTime FinalizacionUtc, int Servicios, int Completados, string? Observaciones);
public sealed record PatologiaInformeDto(string Descripcion, int Cantidad);
public sealed record InformeDto(TipoInforme Tipo, DateOnly Desde, DateOnly Hasta, DateTime? AuditoriaDesdeUtc,
    int Total, IReadOnlyList<SolicitudesPorDiaDto> Solicitudes, IReadOnlyList<AtencionInformeDto> Atenciones,
    IReadOnlyList<PatologiaInformeDto> Patologias);

public sealed class InformePaginaDto
{
    public TipoInforme Tipo { get; set; }
    public PeriodoInformeDto Periodo { get; set; } = new();
    public InformeDto? Informe { get; set; }
}
