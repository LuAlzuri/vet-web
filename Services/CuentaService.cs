using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Dtos;
using VetWeb.Models;

namespace VetWeb.Services;

public class CuentaService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _usuarios;
    public CuentaService(ApplicationDbContext context, UserManager<ApplicationUser> usuarios)
    { _context = context; _usuarios = usuarios; }

    private static void Comprobar(IdentityResult resultado)
    {
        if (!resultado.Succeeded)
            throw new InvalidOperationException("No se pudo guardar la cuenta. Verificá que el correo no esté en uso y que la contraseña tenga 8 caracteres, mayúscula, minúscula, número y símbolo.");
    }

    public async Task GuardarAsync(CuentaDto dto, bool veterinario)
    {
        if (veterinario && dto.Matricula is null) throw new InvalidOperationException("La matrícula es obligatoria.");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Tutor? tutor = null;
        Veterinario? profesional = null;
        ApplicationUser? usuario;
        if (dto.Id != 0)
        {
            if (veterinario) profesional = await _context.Veterinarios.Include(x => x.Usuario).SingleOrDefaultAsync(x => x.Id == dto.Id);
            else tutor = await _context.Tutores.Include(x => x.Usuario).SingleOrDefaultAsync(x => x.Id == dto.Id);
            usuario = veterinario ? profesional?.Usuario : tutor?.Usuario;
            if (usuario is null || !usuario.Activo) throw new InvalidOperationException("La cuenta no está disponible.");
        }
        else
        {
            usuario = await _usuarios.FindByEmailAsync(dto.Email.Trim());
            if (dto.VincularExistente)
            {
                if (usuario is null || !usuario.Activo) throw new InvalidOperationException("No hay una cuenta activa con ese correo.");
                if (veterinario && await _context.Veterinarios.IgnoreQueryFilters().AnyAsync(x => x.UserId == usuario.Id)
                    || !veterinario && await _context.Tutores.IgnoreQueryFilters().AnyAsync(x => x.UserId == usuario.Id))
                    throw new InvalidOperationException("La cuenta ya tiene ese perfil, activo o dado de baja.");
            }
            else
            {
                if (usuario is not null) throw new InvalidOperationException("El correo ya existe. Marcá la vinculación si querés agregar otro perfil a esa misma cuenta.");
                if (string.IsNullOrWhiteSpace(dto.Password)) throw new InvalidOperationException("La contraseña inicial es obligatoria para una cuenta nueva.");
                usuario = new ApplicationUser { UserName = dto.Email.Trim(), Email = dto.Email.Trim(), Nombre = dto.Nombre.Trim(), Apellido = dto.Apellido.Trim() };
                Comprobar(await _usuarios.CreateAsync(usuario, dto.Password));
            }
        }
        // Al vincular otro perfil se conservan los datos y contraseña de la cuenta existente.
        if (dto.Id != 0)
        {
            if (!string.Equals(usuario.Email, dto.Email.Trim(), StringComparison.OrdinalIgnoreCase)) usuario.EmailConfirmed = false;
            usuario.Nombre = dto.Nombre.Trim(); usuario.Apellido = dto.Apellido.Trim();
            usuario.Email = dto.Email.Trim(); usuario.UserName = dto.Email.Trim();
            Comprobar(await _usuarios.UpdateAsync(usuario));
        }
        if (dto.Id == 0)
        {
            Comprobar(await _usuarios.AddToRoleAsync(usuario, veterinario ? "Veterinario" : "Tutor"));
            if (veterinario) _context.Veterinarios.Add(new Veterinario { UserId = usuario.Id, Matricula = dto.Matricula!.Value });
            else _context.Tutores.Add(new Tutor { UserId = usuario.Id });
        }
        else if (veterinario) profesional!.Matricula = dto.Matricula!.Value;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task EliminarAsync(int id, bool veterinario, string usuarioActualId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var tutor = veterinario ? null : await _context.Tutores.Include(x => x.Usuario).SingleOrDefaultAsync(x => x.Id == id);
        var profesional = veterinario ? await _context.Veterinarios.Include(x => x.Usuario).SingleOrDefaultAsync(x => x.Id == id) : null;
        var usuario = veterinario ? profesional?.Usuario : tutor?.Usuario;
        if (usuario is null) throw new InvalidOperationException("No se encontró la cuenta.");
        if (usuario.Id == usuarioActualId) throw new InvalidOperationException("No podés dar de baja tu propia cuenta desde esta pantalla.");
        if (await _usuarios.IsInRoleAsync(usuario, "Veterinario") && !await _context.Veterinarios.AnyAsync(x => x.UserId != usuario.Id && x.Usuario.Activo))
            throw new InvalidOperationException("No se puede dar de baja al último veterinario activo.");
        if (await _context.Turnos.IgnoreQueryFilters().AnyAsync(x =>
            (x.Perro.Tutor.UserId == usuario.Id || x.Veterinario != null && x.Veterinario.UserId == usuario.Id)
            && (x.EstadoId == EstadosTurno.Solicitado || x.EstadoId == EstadosTurno.Confirmado)))
            throw new InvalidOperationException("Cancelá o completá los turnos activos de esta cuenta antes de darle de baja.");
        usuario.Activo = false;
        if (tutor is not null) tutor.Eliminado = true;
        if (profesional is not null) profesional.Eliminado = true;
        Comprobar(await _usuarios.UpdateSecurityStampAsync(usuario));
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
