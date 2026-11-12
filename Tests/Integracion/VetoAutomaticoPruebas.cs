using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

internal static partial class VetoAutomaticoPruebas
{
    private static void Exigir(bool valor, string mensaje)
    {
        if (!valor) throw new Exception("Veto automático: " + mensaje);
    }

    public static async Task EjecutarAsync(ServiceProvider provider, string password, string veterinarioId)
    {
        async Task<T> Con<T>(Func<IServiceProvider, Task<T>> accion)
        {
            await using var scope = provider.CreateAsyncScope();
            return await accion(scope.ServiceProvider);
        }
        async Task Ejecutar(Func<IServiceProvider, Task> accion) =>
            await Con(async sp => { await accion(sp); return true; });
        async Task<(int Tutor, int Perro, string Usuario)> CrearTutor()
        {
            return await Con(async sp =>
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                var email = $"veto-{Guid.NewGuid():N}@prueba.invalid";
                await sp.GetRequiredService<CuentaService>().GuardarAsync(new CuentaDto
                { Nombre = "Veto", Apellido = "Automático", Email = email, Password = password }, false);
                var tutor = await db.Tutores.SingleAsync(x => x.Usuario.Email == email);
                var perro = new Perro { TutorId = tutor.Id, Nombre = "Perro veto", FechaNacimiento = Reloj.Hoy.AddYears(-2),
                    GrupoEtarioId = await db.GruposEtarios.Select(x => x.Id).FirstAsync(),
                    TamanioId = await db.Tamanios.Select(x => x.Id).FirstAsync() };
                db.Perros.Add(perro); await db.SaveChangesAsync();
                return (tutor.Id, perro.Id, tutor.UserId);
            });
        }
        async Task<int> Reservar(int perro, string usuario, bool presencial = false)
        {
            return await Con(async sp =>
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                // Cada reserva tiene su propia fecha para no disparar la regla de duplicados.
                var fecha = (await db.CuposDiarios.MaxAsync(x => (DateTime?)x.FechaCupo) ?? Reloj.Hoy).AddDays(1);
                var cupo = new CupoDiario { FechaCupo = fecha, NumeroCuposDisponibles = 3 };
                db.CuposDiarios.Add(cupo); await db.SaveChangesAsync();
                var dto = new TurnoDto { PerroId = perro, CupoDiarioId = cupo.Id,
                    ServicioIds = new() { await db.Servicios.Select(x => x.Id).FirstAsync() } };
                var turnos = sp.GetRequiredService<TurnoService>();
                return presencial ? await turnos.ReservarPresencialAsync(dto, usuario)
                    : await turnos.GuardarAsync(dto, usuario, false);
            });
        }
        Task Cambiar(int turno, int destino, string usuario, bool vet = false) => Ejecutar(async sp =>
        {
            var servicio = sp.GetRequiredService<TurnoService>();
            if (destino == EstadosTurno.Completado)
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                await db.CuposDiarios.Where(x => db.Turnos.Any(t => t.Id == turno && t.CupoDiarioId == x.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.FechaCupo, Reloj.Hoy));
                await servicio.CompletarAsync(turno, new CompletarTurnoDto(), usuario);
            }
            else await servicio.CambiarEstadoAsync(turno, destino, usuario, vet);
        });
        Task<int> Vigentes(int tutor) => Con(sp => sp.GetRequiredService<VetoService>().Vigentes().CountAsync(x => x.TutorId == tutor));
        async Task Rechazar(Func<Task> accion)
        {
            try { await accion(); } catch (InvalidOperationException) { return; }
            throw new Exception("Veto automático: se aceptó una operación inválida.");
        }
        async Task Cancelar((int Tutor, int Perro, string Usuario) tutor)
        {
            var turno = await Reservar(tutor.Perro, tutor.Usuario);
            await Cambiar(turno, EstadosTurno.CanceladoTutor, tutor.Usuario);
        }
        async Task Levantar(int tutor)
        {
            await Ejecutar(async sp =>
            {
                var vetos = sp.GetRequiredService<VetoService>();
                var id = await vetos.Vigentes().Where(x => x.TutorId == tutor).Select(x => x.Id).SingleAsync();
                await vetos.LevantarAsync(tutor, id, veterinarioId, "Reinicio de prueba");
            });
        }

        var a = await CrearTutor(); var b = await CrearTutor();
        // Rechazos y cancelaciones veterinarias no cuentan.
        foreach (var estado in new[] { EstadosTurno.Rechazado, EstadosTurno.CanceladoVeterinario })
        {
            var turno = await Reservar(a.Perro, a.Usuario);
            await Cambiar(turno, estado, veterinarioId, true);
        }
        await Cancelar(a); Exigir(await Vigentes(a.Tutor) == 0, "la primera cancelación vetó al tutor.");
        var segunda = await Reservar(a.Perro, a.Usuario);
        await Cambiar(segunda, EstadosTurno.CanceladoTutor, a.Usuario);
        await Rechazar(() => Cambiar(segunda, EstadosTurno.CanceladoTutor, a.Usuario));
        Exigir(await Vigentes(a.Tutor) == 0, "la segunda cancelación o el intento repetido aplicaron veto.");
        await Cancelar(b); Exigir(await Vigentes(b.Tutor) == 0, "el conteo incluyó otro tutor.");
        await Cancelar(a); Exigir(await Vigentes(a.Tutor) == 1, "la tercera cancelación no aplicó exactamente un veto.");
        await Con(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var veto = await db.Vetos.SingleAsync(x => x.TutorId == a.Tutor);
            Exigir(veto.VeterinarioId is null && veto.Motivo == Veto.MotivoAutomatico, "origen o motivo incorrectos.");
            var evento = await db.AuditoriaAcciones.SingleAsync(x => x.Entidad == nameof(Veto) && x.EntidadId == veto.Id);
            Exigir(evento.Accion == AuditoriaAccion.VetoAutomaticoAplicado && evento.RolActivo == "Tutor"
                && evento.UsuarioId == a.Usuario, "la auditoría atribuyó el veto a una acción veterinaria.");
            return true;
        });
        await Rechazar(async () => { await Reservar(a.Perro, a.Usuario); });
        var presencial = await Reservar(a.Perro, veterinarioId, true);
        await Cambiar(presencial, EstadosTurno.CanceladoVeterinario, veterinarioId, true);
        Exigir(await Vigentes(a.Tutor) == 1, "la reserva presencial alteró el veto.");
        await Levantar(a.Tutor);
        await Cancelar(a); await Cancelar(a);
        Exigir(await Vigentes(a.Tutor) == 0, "levantar el veto no reinició el conteo.");
        await Cancelar(a); Exigir(await Vigentes(a.Tutor) == 1, "no se volvió a aplicar en la tercera cancelación nueva.");
        await Levantar(a.Tutor);

        // Completar una atención reinicia, confirmar no lo hace.
        await Cancelar(a); await Cancelar(a);
        var completado = await Reservar(a.Perro, a.Usuario);
        await Cambiar(completado, EstadosTurno.Confirmado, veterinarioId, true);
        await Cambiar(completado, EstadosTurno.Completado, veterinarioId, true);
        await Cancelar(a); await Cancelar(a);
        Exigir(await Vigentes(a.Tutor) == 0, "completar una atención no reinició el conteo.");
        await Cancelar(a); Exigir(await Vigentes(a.Tutor) == 1, "el tercer evento posterior a completar no aplicó veto.");
        await Levantar(a.Tutor);

        // Mismo timestamp: el ID decide qué cancelaciones sucedieron después del reinicio.
        var empate = await CrearTutor();
        await Cancelar(empate); await Cancelar(empate);
        var atencion = await Reservar(empate.Perro, empate.Usuario);
        await Cambiar(atencion, EstadosTurno.Confirmado, veterinarioId, true);
        await Cambiar(atencion, EstadosTurno.Completado, veterinarioId, true);
        await Cancelar(empate); await Cancelar(empate);
        await Ejecutar(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var fecha = DateTime.UtcNow.AddMinutes(-1);
            await db.AuditoriaAcciones.Where(x => x.TutorId == empate.Tutor).ExecuteUpdateAsync(s => s.SetProperty(x => x.FechaUtc, fecha));
        });
        Exigir(await Vigentes(empate.Tutor) == 0, "el empate inicial produjo veto prematuro.");
        await Cancelar(empate); Exigir(await Vigentes(empate.Tutor) == 1, "el orden por ID no resolvió el empate.");

        // Dos cancelaciones distintas a partir de dos previas: ambas prosperan y solo un veto.
        await Cancelar(a); await Cancelar(a);
        var c1 = await Reservar(a.Perro, a.Usuario); var c2 = await Reservar(a.Perro, a.Usuario);
        await using (var bloqueo = provider.CreateAsyncScope())
        {
            var db = bloqueo.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await using var transaccion = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Tutores\" WHERE \"Id\" = {a.Tutor} FOR UPDATE");
            var primera = Cambiar(c1, EstadosTurno.CanceladoTutor, a.Usuario);
            var segundaConcurrente = Cambiar(c2, EstadosTurno.CanceladoTutor, a.Usuario);
            var esperando = 0;
            try
            {
                for (var intento = 0; intento < 250 && esperando < 2; intento++)
                {
                    esperando = await Con(sp => sp.GetRequiredService<ApplicationDbContext>().Database.SqlQueryRaw<int>("""
                        SELECT count(*)::int AS "Value" FROM pg_stat_activity
                        WHERE datname = current_database() AND wait_event_type = 'Lock' AND query LIKE '%Tutores%'
                        """).SingleAsync());
                    if (esperando < 2) await Task.Delay(20);
                }
            }
            finally { await transaccion.CommitAsync(); }
            await Task.WhenAll(primera, segundaConcurrente);
            Exigir(esperando >= 2, "no se solaparon las cancelaciones en PostgreSQL.");
        }
        Exigir(await Vigentes(a.Tutor) == 1, "la concurrencia generó vetos duplicados.");
        await Con(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            Exigir(await db.CuposDiarios.Where(x => db.Turnos.Any(t => (t.Id == c1 || t.Id == c2) && t.CupoDiarioId == x.Id))
                .AllAsync(x => x.NumeroCuposDisponibles == 3), "la concurrencia devolvió plazas incorrectamente.");
            Exigir(await db.AuditoriaAcciones.CountAsync(x => x.TutorId == a.Tutor
                && x.Accion == AuditoriaAccion.VetoAutomaticoAplicado) == 4, "la auditoría automática se duplicó.");
            return true;
        });
        await Levantar(a.Tutor);
        var repetida = await Reservar(a.Perro, a.Usuario);
        async Task<bool> Intentar()
        {
            try { await Cambiar(repetida, EstadosTurno.CanceladoTutor, a.Usuario); return true; }
            catch (InvalidOperationException) { return false; }
        }
        var resultados = await Task.WhenAll(Intentar(), Intentar());
        Exigir(resultados.Count(x => x) == 1, "dos cancelaciones del mismo turno prosperaron.");
        await Con(async sp =>
        {
            var db = sp.GetRequiredService<ApplicationDbContext>();
            Exigir(await db.Turnos.Where(x => x.Id == repetida).Select(x => x.CupoDiario.NumeroCuposDisponibles).SingleAsync() == 3,
                "se devolvieron plazas dos veces.");
            Exigir(await db.AuditoriaAcciones.CountAsync(x => x.Entidad == nameof(Turno) && x.EntidadId == repetida
                && x.EstadoNuevoId == EstadosTurno.CanceladoTutor) == 1, "se auditó un intento fallido.");
            return true;
        });
        // Fallar al auditar el veto debe revertir también cancelación y devolución.
        await Cancelar(a); // Segunda cancelación desde el último levantamiento.
        var fallida = await Reservar(a.Perro, a.Usuario);
        await Ejecutar(sp => sp.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fallar_auditoria_veto() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."Accion" = 'VetoAutomaticoAplicado' THEN
                    RAISE EXCEPTION 'Fallo intencional de prueba';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER fallar_auditoria_veto BEFORE INSERT ON "AuditoriaAcciones"
                FOR EACH ROW EXECUTE FUNCTION fallar_auditoria_veto();
            """));
        try
        {
            var fallo = false;
            try { await Cambiar(fallida, EstadosTurno.CanceladoTutor, a.Usuario); }
            catch (DbUpdateException) { fallo = true; }
            Exigir(fallo, "la prueba de rollback no provocó el fallo esperado.");
            await Con(async sp =>
            {
                var db = sp.GetRequiredService<ApplicationDbContext>();
                var turno = await db.Turnos.Include(x => x.CupoDiario).SingleAsync(x => x.Id == fallida);
                Exigir(turno.EstadoId == EstadosTurno.Solicitado && turno.CupoDiario.NumeroCuposDisponibles == 2,
                    "el fallo de auditoría no revirtió estado y plazas.");
                Exigir(!await db.Vetos.AnyAsync(x => x.TutorId == a.Tutor && x.FechaFin == null), "quedó un veto parcial.");
                Exigir(!await db.AuditoriaAcciones.AnyAsync(x => x.Entidad == nameof(Turno) && x.EntidadId == fallida
                    && x.EstadoNuevoId == EstadosTurno.CanceladoTutor), "quedó una cancelación auditada sin confirmar.");
                return true;
            });
        }
        finally
        {
            await Ejecutar(sp => sp.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlRawAsync("""
                DROP TRIGGER fallar_auditoria_veto ON "AuditoriaAcciones";
                DROP FUNCTION fallar_auditoria_veto();
                """));
        }
        await Cambiar(fallida, EstadosTurno.CanceladoTutor, a.Usuario);
        Exigir(await Vigentes(a.Tutor) == 1, "no se pudo reintentar tras el rollback.");
        Console.WriteLine("Veto automático: umbral, reinicios, exclusiones, aislamiento, empate por fecha/ID, concurrencia, devolución única y rollback atómico verificados.");
    }
}
