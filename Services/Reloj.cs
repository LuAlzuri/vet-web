namespace VetWeb.Services;
public static class Reloj
{
    public static DateTime Hoy => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires")).Date;
}
