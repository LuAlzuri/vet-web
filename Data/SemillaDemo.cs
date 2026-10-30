using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Data;

public static class SemillaDemo
{
    // Credencial pública de demostración. Este comando solo se permite en Development.
    public const string Password = "DemoVet2026!";
    public const string EmailVeterinario = "veterinario.demo@vetweb.invalid";
    public const string EmailTutor = "tutor.demo@vetweb.invalid";
    public const string EmailDoble = "doble.demo@vetweb.invalid";

    public static async Task CrearAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var usuarios = services.GetRequiredService<UserManager<ApplicationUser>>();
        var cuentas = services.GetRequiredService<CuentaService>();
        var turnos = services.GetRequiredService<TurnoService>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Aplicá las migraciones antes de ejecutar la semilla demo.");

        async Task<ApplicationUser> Cuenta(string email, string nombre, bool veterinario, bool tutor)
        {
            var usuario = await usuarios.FindByEmailAsync(email);
            if (usuario is not null && (usuario.Nombre != nombre || usuario.Apellido != "Demo"))
                throw new InvalidOperationException("Un correo reservado para la semilla ya pertenece a otra cuenta. No se modificará.");
            if (usuario is not null && !usuario.Activo)
                throw new InvalidOperationException("Una cuenta demo está dada de baja. La semilla no reactiva cuentas ni cambia contraseñas.");

            if (usuario is null)
            {
                await cuentas.GuardarAsync(new CuentaDto { Nombre = nombre, Apellido = "Demo", Email = email,
                    Password = Password, Matricula = veterinario && !tutor ? 90001 : null }, veterinario && !tutor);
                usuario = (await usuarios.FindByEmailAsync(email))!;
            }
            if (tutor && !await db.Tutores.IgnoreQueryFilters().AnyAsync(x => x.UserId == usuario.Id))
                await cuentas.GuardarAsync(new CuentaDto { Nombre = nombre, Apellido = "Demo", Email = email, VincularExistente = true }, false);
            if (veterinario && !await db.Veterinarios.IgnoreQueryFilters().AnyAsync(x => x.UserId == usuario.Id))
                await cuentas.GuardarAsync(new CuentaDto { Nombre = nombre, Apellido = "Demo", Email = email, VincularExistente = true, Matricula = 90002 }, true);
            if (await usuarios.IsInRoleAsync(usuario, "Veterinario") != veterinario || await usuarios.IsInRoleAsync(usuario, "Tutor") != tutor)
                throw new InvalidOperationException("Los roles de una cuenta demo fueron modificados. La semilla no los reemplaza.");
            return usuario;
        }

        var veterinario = await Cuenta(EmailVeterinario, "Marina", true, false);
        var tutor = await Cuenta(EmailTutor, "Tomás", false, true);
        var doble = await Cuenta(EmailDoble, "Alex", true, true);
        var profesional = await db.Veterinarios.SingleAsync(x => x.UserId == veterinario.Id);
        var tutorPerfil = await db.Tutores.SingleAsync(x => x.UserId == tutor.Id);
        var doblePerfil = await db.Tutores.SingleAsync(x => x.UserId == doble.Id);

        var grupo = await db.GruposEtarios.FirstOrDefaultAsync(x => x.Descripcion == "Demo - Adulto");
        if (grupo is null) { grupo = new GrupoEtario { Descripcion = "Demo - Adulto" }; db.GruposEtarios.Add(grupo); }
        var tamanio = await db.Tamanios.FirstOrDefaultAsync(x => x.Descripcion == "Demo - Mediano");
        if (tamanio is null) { tamanio = new Tamanio { Descripcion = "Demo - Mediano" }; db.Tamanios.Add(tamanio); }
        var servicio = await db.Servicios.FirstOrDefaultAsync(x => x.Descripcion == "Demo - Baño");
        if (servicio is null) { servicio = new Servicio { Descripcion = "Demo - Baño" }; db.Servicios.Add(servicio); }
        await db.SaveChangesAsync();

        async Task<Perro> Mascota(string nombre, int tutorId)
        {
            var perro = await db.Perros.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Nombre == nombre && x.TutorId == tutorId);
            if (perro is not null)
            {
                if (perro.Eliminado) throw new InvalidOperationException("Una mascota demo fue dada de baja. No se reactivará automáticamente.");
                return perro;
            }
            perro = new Perro { Nombre = nombre, TutorId = tutorId, GrupoEtarioId = grupo.Id, TamanioId = tamanio.Id,
                FechaNacimiento = Reloj.Hoy.AddYears(-3), CalendarioVacunasCompleto = true };
            db.Perros.Add(perro); await db.SaveChangesAsync(); return perro;
        }
        var luna = await Mascota("Luna (demo)", tutorPerfil.Id);
        var milo = await Mascota("Milo (demo)", doblePerfil.Id);
        var mascotas = new[] { luna, milo };
        CupoDiario? cupo = null;
        foreach (var perro in mascotas)
        {
            var marca = "Semilla demo: " + perro.Nombre;
            if (await db.Turnos.IgnoreQueryFilters().AnyAsync(x => x.PerroId == perro.Id && x.Observaciones == marca)) continue;
            if (cupo is null)
            {
                // Crear una fecha libre evita consumir o modificar cupos de trabajo existentes.
                var fecha = Reloj.Hoy.AddDays(1);
                while (await db.CuposDiarios.AnyAsync(x => x.FechaCupo == fecha)) fecha = fecha.AddDays(1);
                cupo = new CupoDiario { FechaCupo = fecha, NumeroCuposDisponibles = 3 };
                db.CuposDiarios.Add(cupo); await db.SaveChangesAsync();
            }
            await turnos.GuardarAsync(new TurnoDto { PerroId = perro.Id, VeterinarioId = profesional.Id,
                CupoDiarioId = cupo.Id, ServicioIds = new List<int> { servicio.Id }, Observaciones = marca }, veterinario.Id, true);
        }
        Console.WriteLine("Semilla lista: veterinario.demo@vetweb.invalid, tutor.demo@vetweb.invalid y doble.demo@vetweb.invalid.");
        Console.WriteLine("Contraseña de demostración para las cuentas nuevas: " + Password);
        Console.WriteLine("Luna pertenece al tutor. Milo pertenece a la cuenta con dos roles. Hay un turno de muestra para cada perro.");
        Console.WriteLine("Las cuentas y registros previos se conservaron. Repetir la semilla no duplica los datos demo.");
    }
}
