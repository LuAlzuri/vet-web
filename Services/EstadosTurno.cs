namespace VetWeb.Services;
public static class EstadosTurno
{
    public const int Solicitado = 1;
    public const int Confirmado = 2;
    public const int Rechazado = 3;
    public const int Completado = 4;
    public const int CanceladoTutor = 5;
    public const int CanceladoVeterinario = 6;
    public static bool Activo(int estado) => estado == Solicitado || estado == Confirmado;
}
