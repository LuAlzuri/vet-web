using Microsoft.Extensions.Caching.Memory;
using VetWeb.Dtos;

namespace VetWeb.Services.Chatbot;

// Una propuesta por usuario y rol activo, con vencimiento y consumo atómico.
// Solo contiene selecciones verificadas por el servidor; no guarda conversaciones.
public sealed class ChatbotReservasPendientes(IMemoryCache cache)
{
    private readonly object bloqueo = new();
    private sealed record Pendiente(string Token, TurnoDto Turno, bool Presencial);
    private static string Clave(string usuario, bool veterinario) => $"chatbot-reserva:{usuario}:{veterinario}";

    public string Preparar(string usuario, bool veterinario, TurnoDto turno, bool presencial)
    {
        var token = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        lock (bloqueo)
            cache.Set(Clave(usuario, veterinario), new Pendiente(token, turno, presencial), TimeSpan.FromMinutes(5));
        return token;
    }

    public void Descartar(string usuario, bool veterinario)
    {
        lock (bloqueo) cache.Remove(Clave(usuario, veterinario));
    }

    public (TurnoDto Turno, bool Presencial) Consumir(string usuario, bool veterinario, string? token)
    {
        lock (bloqueo)
        {
            var clave = Clave(usuario, veterinario);
            if (!cache.TryGetValue<Pendiente>(clave, out var pendiente) || pendiente is null || pendiente.Token != token)
                throw new InvalidOperationException("La propuesta venció o ya fue utilizada. Prepará una nueva reserva.");
            cache.Remove(clave);
            return (pendiente.Turno, pendiente.Presencial);
        }
    }
}
