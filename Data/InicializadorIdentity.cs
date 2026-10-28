using Microsoft.AspNetCore.Identity;
using VetWeb.Models;

namespace VetWeb.Data;

public static class InicializadorIdentity
{
    public static async Task CrearVeterinarioAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["VeterinarioInicial:Email"]?.Trim();
        var nombre = configuration["VeterinarioInicial:Nombre"]?.Trim();
        var apellido = configuration["VeterinarioInicial:Apellido"]?.Trim();
        var password = configuration["VeterinarioInicial:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(nombre)
            || string.IsNullOrWhiteSpace(apellido) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Configurá Email, Nombre, Apellido y Password en VeterinarioInicial mediante secretos de usuario.");

        if (nombre.Length > 50 || apellido.Length > 50)
            throw new InvalidOperationException("Nombre y Apellido no pueden superar los 50 caracteres.");

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var context = services.GetRequiredService<ApplicationDbContext>();

        if (!await roleManager.RoleExistsAsync("Veterinario"))
            throw new InvalidOperationException("Falta el rol Veterinario. Aplicá primero la migración InicialIdentity.");

        var existente = await userManager.FindByEmailAsync(email);
        if (existente is not null)
        {
            if (!await userManager.IsInRoleAsync(existente, "Veterinario"))
                throw new InvalidOperationException("El correo ya pertenece a otra cuenta. No se modificó su rol.");

            Console.WriteLine("La cuenta ya tiene el rol Veterinario. No se modificaron sus datos ni su contraseña.");
            return;
        }

        // Crear la cuenta y asignar el rol como una sola operación.
        await using var transaction = await context.Database.BeginTransactionAsync();
        var usuario = new ApplicationUser
        {
            UserName = email,
            Email = email,
            Nombre = nombre,
            Apellido = apellido
        };

        var resultado = await userManager.CreateAsync(usuario, password);
        if (!resultado.Succeeded)
            throw new InvalidOperationException("No se pudo crear la cuenta. Revisá el formato del correo y la política de contraseña. Códigos: "
                + string.Join(", ", resultado.Errors.Select(error => error.Code)));

        var asignacion = await userManager.AddToRoleAsync(usuario, "Veterinario");
        if (!asignacion.Succeeded)
            throw new InvalidOperationException("No se pudo asignar el rol Veterinario. Se revierte la creación de la cuenta.");

        await transaction.CommitAsync();
        Console.WriteLine("Cuenta inicial creada con el rol Veterinario.");
    }
}
