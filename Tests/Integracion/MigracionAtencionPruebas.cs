using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VetWeb.Data;
using VetWeb.Models;
using VetWeb.Services;

internal static class MigracionAtencionPruebas
{
    public static async Task EjecutarAsync(ApplicationDbContext db)
    {
        var migrador = db.GetService<IMigrator>();
        await migrador.MigrateAsync("20261112005953_AgregarAuditoriaAccion");
        var usuario = new ApplicationUser { Nombre = "Migración", Apellido = "Atención", UserName = "migracion@prueba.invalid" };
        var tutor = new Tutor { Usuario = usuario };
        var grupo = new GrupoEtario { Descripcion = "Grupo de migración" };
        var tamanio = new Tamanio { Descripcion = "Tamaño de migración" };
        var perro = new Perro { Nombre = "Perro de migración", Tutor = tutor, GrupoEtario = grupo, Tamanio = tamanio, FechaNacimiento = Reloj.Hoy.AddYears(-2) };
        var cupo = new CupoDiario { FechaCupo = Reloj.Hoy, NumeroCuposDisponibles = 1 };
        var tratamiento = new Tratamiento { Descripcion = "Tratamiento previo a migración" };
        var turno = new Turno { Perro = perro, CupoDiario = cupo, EstadoId = EstadosTurno.Completado };
        db.AddRange(turno, tratamiento); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Turnos\" SET \"TratamientoId\" = {tratamiento.Id} WHERE \"Id\" = {turno.Id}");
        await migrador.MigrateAsync();
        var relacion = await db.TratamientosPorTurno.SingleAsync();
        if (relacion.TurnoId != turno.Id || relacion.TratamientoId != tratamiento.Id || relacion.Razon is not null)
            throw new Exception("La migración no preservó el tratamiento anterior.");
        // Sin motivos ni múltiples tratamientos, el retorno al modelo anterior también conserva el dato.
        db.ChangeTracker.Clear();
        await migrador.MigrateAsync("20261112005953_AgregarAuditoriaAccion");
        var anterior = await db.Database.SqlQueryRaw<int>("SELECT \"TratamientoId\" AS \"Value\" FROM \"Turnos\"").SingleAsync();
        if (anterior != tratamiento.Id) throw new Exception("La reversión perdió el tratamiento anterior.");
        await migrador.MigrateAsync();
        await db.TratamientosPorTurno.ExecuteUpdateAsync(s => s.SetProperty(x => x.Razon, "Motivo que no puede perderse"));
        var bloqueada = false;
        try { await migrador.MigrateAsync("20261112005953_AgregarAuditoriaAccion"); }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "P0001") { bloqueada = true; }
        if (!bloqueada || !await db.TratamientosPorTurno.AnyAsync(x => x.Razon == "Motivo que no puede perderse"))
            throw new Exception("La reversión permitió perder un motivo de tratamiento.");
        await db.TratamientosPorTurno.ExecuteDeleteAsync();
        await db.Turnos.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Perros.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Tutores.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.CuposDiarios.ExecuteDeleteAsync();
        await db.Tratamientos.ExecuteDeleteAsync();
        await db.GruposEtarios.ExecuteDeleteAsync();
        await db.Tamanios.ExecuteDeleteAsync();
        Console.WriteLine("Migración de atención: actualización con tratamiento previo, reversión compatible y nueva instalación verificadas.");
    }
}
