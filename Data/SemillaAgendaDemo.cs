using Microsoft.EntityFrameworkCore;
using VetWeb.Dtos;
using VetWeb.Services;
using VetWeb.Models;

namespace VetWeb.Data;

public static class SemillaAgendaDemo
{
    public static async Task CrearAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var turnos = services.GetRequiredService<TurnoService>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Aplicá las migraciones antes de cargar la agenda demo.");
        var alex = await db.Veterinarios.Include(x => x.Usuario)
            .SingleAsync(x => x.Usuario.Email == SemillaDemo.EmailDoble && x.Usuario.Activo);
        var marina = await db.Veterinarios.Include(x => x.Usuario)
            .SingleAsync(x => x.Usuario.Email == SemillaDemo.EmailVeterinario && x.Usuario.Activo);
        var servicio = await db.Servicios.OrderBy(x => x.Id).FirstAsync();
        var escenarios = new[] {
            (Email: "lucia.demo@vetweb.invalid", Perro: "Nala (demo)", Vet: (Veterinario?)alex, Confirmado: false),
            (Email: "lucia.demo@vetweb.invalid", Perro: "Toby (demo)", Vet: (Veterinario?)alex, Confirmado: true),
            (Email: "martin.demo@vetweb.invalid", Perro: "Mora (demo)", Vet: (Veterinario?)null, Confirmado: false),
            (Email: "martin.demo@vetweb.invalid", Perro: "Simón (demo)", Vet: (Veterinario?)null, Confirmado: true),
            (Email: "sofia.demo@vetweb.invalid", Perro: "Olivia (demo)", Vet: (Veterinario?)marina, Confirmado: false),
            (Email: "sofia.demo@vetweb.invalid", Perro: "Roco (demo)", Vet: (Veterinario?)marina, Confirmado: true)
        };
        // Validar todas las mascotas antes de reservar cupos.
        foreach (var escenario in escenarios)
            if (!await db.Perros.AnyAsync(x => x.Nombre == escenario.Perro && x.Tutor.Usuario.Email == escenario.Email && x.Tutor.Usuario.Activo))
                throw new InvalidOperationException("Primero cargá los tutores y perros con --semilla-tutores.");

        var nuevos = 0;
        CupoDiario? cupo = null;
        for (var i = 0; i < escenarios.Length; i++)
        {
            if (i % 2 == 0) cupo = null;
            var escenario = escenarios[i];
            var perro = await db.Perros.SingleAsync(x => x.Nombre == escenario.Perro && x.Tutor.Usuario.Email == escenario.Email);
            var marca = "Agenda demo Alex: " + escenario.Perro;
            var turno = await db.Turnos.IgnoreQueryFilters().Include(x => x.CupoDiario)
                .SingleOrDefaultAsync(x => x.PerroId == perro.Id && x.Observaciones == marca);
            if (turno is null)
            {
                if (cupo is null)
                {
                    // Fechas nuevas: no se consumen ni editan cupos de trabajo anteriores.
                    var fecha = Reloj.Hoy.AddDays(1);
                    while (await db.CuposDiarios.AnyAsync(x => x.FechaCupo == fecha)) fecha = fecha.AddDays(1);
                    cupo = new CupoDiario { FechaCupo = fecha, NumeroCuposDisponibles = 4 };
                    db.CuposDiarios.Add(cupo);
                    await db.SaveChangesAsync();
                }
                await turnos.GuardarAsync(new TurnoDto { PerroId = perro.Id, VeterinarioId = escenario.Vet?.Id,
                    CupoDiarioId = cupo.Id, ServicioIds = new List<int> { servicio.Id }, Observaciones = marca }, alex.UserId, true);
                turno = await db.Turnos.Include(x => x.CupoDiario).SingleAsync(x => x.PerroId == perro.Id && x.Observaciones == marca);
                if (escenario.Confirmado)
                    await turnos.CambiarEstadoAsync(turno.Id, EstadosTurno.Confirmado, escenario.Vet?.UserId ?? alex.UserId, true);
                nuevos++;
            }
            var visible = await turnos.PuedeAtenderAsync(turno, alex.UserId);
            if (visible != (escenario.Vet is null || escenario.Vet.Id == alex.Id))
                throw new InvalidOperationException("La visibilidad de la agenda demo no coincide con el profesional asignado.");
            Console.WriteLine($"{turno.CupoDiario.FechaCupo:dd/MM/yyyy} | {escenario.Perro} | {escenario.Vet?.Usuario.Nombre ?? "No especifica"} | estado {turno.EstadoId} | visible para Alex: {visible}");
        }
        Console.WriteLine($"Agenda demo lista: {nuevos} turnos nuevos. Los turnos anteriores y sus estados se conservaron.");
    }
}
