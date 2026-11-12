namespace VetWeb.Services;
public static class Reloj
{
    public static DateTime Hoy => EnArgentina(DateTime.UtcNow).Date;

    public static DateTime EnArgentina(DateTime fechaUtc) => TimeZoneInfo.ConvertTimeFromUtc(fechaUtc,
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"));
}
