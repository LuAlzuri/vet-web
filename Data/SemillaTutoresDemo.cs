using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetWeb.Dtos;
using VetWeb.Models;
using VetWeb.Services;

namespace VetWeb.Data;

public static class SemillaTutoresDemo
{
    public static async Task CrearAsync(IServiceProvider services, bool actualizarFotos = false)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var usuarios = services.GetRequiredService<UserManager<ApplicationUser>>();
        var cuentas = services.GetRequiredService<CuentaService>();
        var imagenes = services.GetRequiredService<ImagenPerroService>();
        var ambiente = services.GetRequiredService<IWebHostEnvironment>();
        if ((await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Aplicá las migraciones antes de cargar los tutores demo.");

        // Usar catálogos existentes; esta semilla no altera sus opciones.
        var grupo = await db.GruposEtarios.SingleAsync(x => x.Descripcion == "Adulto");
        var tamanio = await db.Tamanios.SingleAsync(x => x.Descripcion == "Mediano");
        var datos = new[] {
            (Email: "lucia.demo@vetweb.invalid", Nombre: "Lucía", Apellido: "Demo", Perros: new[] { "Nala (demo)", "Toby (demo)" }),
            (Email: "martin.demo@vetweb.invalid", Nombre: "Martín", Apellido: "Demo", Perros: new[] { "Mora (demo)", "Simón (demo)" }),
            (Email: "sofia.demo@vetweb.invalid", Nombre: "Sofía", Apellido: "Demo", Perros: new[] { "Olivia (demo)", "Roco (demo)" })
        };
        // Validar todas las fotos antes de crear registros.
        var fotos = new List<ImagenPerroService.FotoValidada>();
        for (var i = 1; i <= 6; i++)
        {
            await using var archivo = File.OpenRead(Path.Combine(ambiente.WebRootPath, "images", "demo", $"perro-{i}.jpg"));
            fotos.Add(await imagenes.ValidarAsync(new FormFile(archivo, 0, archivo.Length, "Foto", $"perro-{i}.jpg")));
        }
        foreach (var dato in datos)
        {
            var existente = await usuarios.FindByEmailAsync(dato.Email);
            if (existente is not null && (!existente.Activo || existente.Nombre != dato.Nombre || existente.Apellido != dato.Apellido
                || !await db.Tutores.AnyAsync(x => x.UserId == existente.Id)))
                throw new InvalidOperationException($"La cuenta {dato.Email} fue modificada. No se reemplazará.");
        }
        var indice = 0;
        var creados = 0;
        var fotosActualizadas = 0;
        foreach (var dato in datos)
        {
            var usuario = await usuarios.FindByEmailAsync(dato.Email);
            if (usuario is null)
            {
                if (actualizarFotos) throw new InvalidOperationException("No existe el tutor demo. La actualización de fotos no crea cuentas.");
                await cuentas.GuardarAsync(new CuentaDto { Nombre = dato.Nombre, Apellido = dato.Apellido,
                    Email = dato.Email, Password = SemillaDemo.Password }, false);
                usuario = (await usuarios.FindByEmailAsync(dato.Email))!;
            }
            var tutor = await db.Tutores.SingleAsync(x => x.UserId == usuario.Id);
            foreach (var nombre in dato.Perros)
            {
                var foto = fotos[indice++ % fotos.Count];
                // Incluir bajas evita recrear mascotas que alguien eliminó deliberadamente.
                var nombreSinMarca = nombre.Replace(" (demo)", "");
                var existente = await db.Perros.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.TutorId == tutor.Id
                    && (x.Nombre == nombre || x.Nombre == nombreSinMarca));
                if (existente is not null)
                {
                    // Solo reemplazar las copias repetidas originales, nunca una foto personalizada.
                    if (actualizarFotos && indice > 3 && !existente.Eliminado && existente.FotoArchivo is not null
                        && File.Exists(imagenes.Ruta(existente.FotoArchivo))
                        && (await File.ReadAllBytesAsync(imagenes.Ruta(existente.FotoArchivo))).AsSpan()
                            .SequenceEqual(fotos[(indice - 1) % 3].Contenido))
                    {
                        var anterior = existente.FotoArchivo;
                        var nueva = await imagenes.GuardarAsync(foto);
                        existente.FotoArchivo = nueva;
                        try { await db.SaveChangesAsync(); }
                        catch { imagenes.Eliminar(nueva); throw; }
                        imagenes.Eliminar(anterior);
                        fotosActualizadas++;
                        Console.WriteLine($"Foto nueva: {nombre}.");
                    }
                    continue;
                }
                if (actualizarFotos) throw new InvalidOperationException("No se encontró el perro demo. La actualización de fotos no crea perros.");
                var archivo = await imagenes.GuardarAsync(foto);
                var perro = new Perro { Nombre = nombre, TutorId = tutor.Id, GrupoEtarioId = grupo.Id,
                    TamanioId = tamanio.Id, FechaNacimiento = Reloj.Hoy.AddYears(-2 - indice),
                    CalendarioVacunasCompleto = true, Reactivo = false, FotoArchivo = archivo };
                db.Perros.Add(perro);
                try { await db.SaveChangesAsync(); }
                catch { imagenes.Eliminar(archivo); throw; }
                creados++;
            }
            var cantidad = await db.Perros.CountAsync(x => x.TutorId == tutor.Id);
            Console.WriteLine($"{dato.Email}: {cantidad} perros activos.");
        }
        Console.WriteLine($"Semilla terminada: {creados} perros nuevos y {fotosActualizadas} fotos actualizadas.");
        var correos = datos.Select(x => x.Email).ToArray();
        var archivos = await db.Perros.Where(x => correos.Contains(x.Tutor.Usuario.Email!))
            .Select(x => x.FotoArchivo).ToListAsync();
        var huellas = new HashSet<string>();
        foreach (var archivo in archivos)
            if (archivo is not null && File.Exists(imagenes.Ruta(archivo)))
                huellas.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(imagenes.Ruta(archivo)))));
        Console.WriteLine($"Verificación: {archivos.Count} perros activos y {huellas.Count} imágenes distintas.");
        Console.WriteLine("Contraseña inicial para las cuentas nuevas: " + SemillaDemo.Password);
    }
}
