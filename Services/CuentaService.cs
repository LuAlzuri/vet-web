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

    public async Task<int> GuardarAsync(CuentaDto dto, bool veterinario, string? administradorId = null)
    {
        ValidacionDto.Comprobar(dto);
        if (veterinario && dto.Matricula is null) throw new InvalidOperationException("La matrícula es obligatoria.");
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var gestionaRoles = veterinario && administradorId is not null &&
            await _context.Users.AnyAsync(x => x.Id == administradorId && x.Activo) &&
            await _context.UserRoles.AnyAsync(x => x.UserId == administradorId &&
                _context.Roles.Any(r => r.Id == x.RoleId && r.Name == RolSesion.Administrador));
        if ((dto.EsAdministrador.HasValue || dto.EsCliente || dto.ClienteId.HasValue) && !gestionaRoles)
            throw new InvalidOperationException("Solo Administración puede gestionar estos perfiles y roles.");
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
            usuario = dto.VincularExistente && veterinario && dto.ClienteId.HasValue
                ? await _context.Tutores.Where(x => x.Id == dto.ClienteId && x.Usuario.Activo)
                    .Select(x => x.Usuario).SingleOrDefaultAsync()
                : await _usuarios.FindByEmailAsync(dto.Email.Trim());
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
        if (!gestionaRoles) await ProtegerCuentaAdministrativaAsync(usuario);
        // Al vincular otro perfil se conservan los datos y contraseña de la cuenta existente.
        if (dto.Id != 0 && !(gestionaRoles && await _context.Tutores.AnyAsync(x => x.UserId == usuario.Id)))
        {
            if (!string.Equals(usuario.Email, dto.Email.Trim(), StringComparison.OrdinalIgnoreCase)) usuario.EmailConfirmed = false;
            usuario.Nombre = dto.Nombre.Trim(); usuario.Apellido = dto.Apellido.Trim();
            usuario.Email = dto.Email.Trim(); usuario.UserName = dto.Email.Trim();
            Comprobar(await _usuarios.UpdateAsync(usuario));
        }
        if (dto.Id == 0)
        {
            Comprobar(await _usuarios.AddToRoleAsync(usuario, veterinario ? "Veterinario" : "Tutor"));
            if (veterinario) { profesional = new Veterinario { UserId = usuario.Id, Matricula = dto.Matricula!.Value }; _context.Veterinarios.Add(profesional); }
            else { tutor = new Tutor { UserId = usuario.Id }; _context.Tutores.Add(tutor); }
        }
        else if (veterinario) profesional!.Matricula = dto.Matricula!.Value;
        if (gestionaRoles)
        {
            if (dto.EsCliente && !await _context.Tutores.IgnoreQueryFilters().AnyAsync(x => x.UserId == usuario.Id))
            {
                if (string.IsNullOrWhiteSpace(dto.Telefono)) throw new InvalidOperationException("Ingresá el teléfono de contacto del cliente.");
                Comprobar(await _usuarios.AddToRoleAsync(usuario, "Tutor"));
                _context.Tutores.Add(new Tutor { UserId = usuario.Id, Telefonos = new List<TelefonoTutor> {
                    new() { NombrePropietario = $"{usuario.Nombre} {usuario.Apellido}", Numero = dto.Telefono.Trim() } } });
            }
            else if (dto.EsCliente && !await _context.Tutores.AnyAsync(x => x.UserId == usuario.Id))
                throw new InvalidOperationException("El perfil de cliente está dado de baja.");
            var esAdmin = await _usuarios.IsInRoleAsync(usuario, RolSesion.Administrador);
            if (esAdmin && dto.EsAdministrador == false)
            {
                await ComprobarBajaAdministradorAsync(usuario, administradorId!);
                Comprobar(await _usuarios.RemoveFromRoleAsync(usuario, RolSesion.Administrador));
                Comprobar(await _usuarios.UpdateSecurityStampAsync(usuario));
            }
            else if (!esAdmin && dto.EsAdministrador == true)
            {
                Comprobar(await _usuarios.AddToRoleAsync(usuario, RolSesion.Administrador));
                Comprobar(await _usuarios.UpdateSecurityStampAsync(usuario));
            }
        }
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return veterinario ? profesional!.Id : tutor!.Id;
    }

    public async Task VincularClienteAsync(int veterinarioId, VeterinarioClienteDto dto)
    {
        ValidacionDto.Comprobar(dto);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var usuario = await _context.Veterinarios.Where(x => x.Id == veterinarioId && x.Usuario.Activo)
            .Select(x => x.Usuario).SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("El veterinario no está disponible.");
        await ProtegerCuentaAdministrativaAsync(usuario);
        if (await _context.Tutores.IgnoreQueryFilters().AnyAsync(x => x.UserId == usuario.Id))
            throw new InvalidOperationException("La cuenta ya tiene un perfil de tutor, activo o dado de baja.");

        Comprobar(await _usuarios.AddToRoleAsync(usuario, "Tutor"));
        var tutor = new Tutor { UserId = usuario.Id };
        if (!string.IsNullOrWhiteSpace(dto.Telefono))
        {
            tutor.Telefonos.Add(new TelefonoTutor
            {
                NombrePropietario = $"{usuario.Nombre} {usuario.Apellido}",
                Numero = dto.Telefono.Trim()
            });
        }
        _context.Tutores.Add(tutor);
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
        if (await _usuarios.IsInRoleAsync(usuario, RolSesion.Administrador))
        {
            var actual = await _usuarios.FindByIdAsync(usuarioActualId);
            if (!veterinario || actual is null || !actual.Activo || !await _usuarios.IsInRoleAsync(actual, RolSesion.Administrador))
                throw new InvalidOperationException("Solo Administración puede dar de baja esta cuenta.");
            await ComprobarBajaAdministradorAsync(usuario, usuarioActualId);
        }
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

    private async Task ProtegerCuentaAdministrativaAsync(ApplicationUser usuario)
    {
        if (await _usuarios.IsInRoleAsync(usuario, RolSesion.Administrador))
            throw new InvalidOperationException("Las cuentas administrativas se gestionan fuera de los formularios de perfiles.");
    }

    private async Task ComprobarBajaAdministradorAsync(ApplicationUser usuario, string actualId)
    {
        if (usuario.Id == actualId) throw new InvalidOperationException("No podés quitarte tu propio rol de administrador ni dar de baja tu cuenta.");
        var administradores = await _usuarios.GetUsersInRoleAsync(RolSesion.Administrador);
        if (!administradores.Any(x => x.Id != usuario.Id && x.Activo))
            throw new InvalidOperationException("Debe existir al menos un administrador activo.");
    }
}
