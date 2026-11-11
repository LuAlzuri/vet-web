using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VetWeb.Models;

namespace VetWeb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Veterinario> Veterinarios { get; set; }
    public DbSet<Tutor> Tutores { get; set; }
    public DbSet<TelefonoTutor> TelefonosTutores { get; set; }
    public DbSet<GrupoEtario> GruposEtarios { get; set; }
    public DbSet<Tamanio> Tamanios { get; set; }
    public DbSet<Patologia> Patologias { get; set; }
    public DbSet<Perro> Perros { get; set; }
    public DbSet<PerroPatologia> PerrosPatologias { get; set; }

    public DbSet<Estado> Estados { get; set; }

    public DbSet<Tratamiento> Tratamientos { get; set; }

    public DbSet<Servicio> Servicios { get; set; }

    public DbSet<CupoDiario> CuposDiarios { get; set; }

    public DbSet<Turno> Turnos { get; set; }

    public DbSet<ServicioPorTurno> ServiciosPorTurno { get; set; }

    public DbSet<Veto> Vetos { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Property(usuario => usuario.Activo).HasDefaultValue(true);

        builder.Entity<Veterinario>(entity =>
        {
            entity.ToTable("Veterinarios", table =>
                table.HasCheckConstraint("CK_Veterinarios_Matricula", "\"Matricula\" BETWEEN 1 AND 99999"));

            entity.HasOne(veterinario => veterinario.Usuario)
                .WithOne()
                .HasForeignKey<Veterinario>(veterinario => veterinario.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(veterinario => veterinario.Eliminado).HasDefaultValue(false);
            entity.HasQueryFilter(veterinario => !veterinario.Eliminado);
        });

        builder.Entity<Tutor>(entity =>
        {
            entity.ToTable("Tutores");

            entity.HasOne(tutor => tutor.Usuario)
                .WithOne()
                .HasForeignKey<Tutor>(tutor => tutor.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(tutor => tutor.Eliminado).HasDefaultValue(false);
            entity.HasQueryFilter(tutor => !tutor.Eliminado);
        });

        builder.Entity<TelefonoTutor>(entity =>
        {
            entity.ToTable("TelefonosTutores");

            entity.HasOne(telefono => telefono.Tutor)
                .WithMany(tutor => tutor.Telefonos)
                .HasForeignKey(telefono => telefono.TutorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Mantener las consultas alineadas con el borrado lógico del tutor.
            entity.HasQueryFilter(telefono => !telefono.Tutor.Eliminado);
        });

        builder.Entity<GrupoEtario>().ToTable("GruposEtarios");
        builder.Entity<Tamanio>().ToTable("Tamanios");
        builder.Entity<Patologia>().ToTable("Patologias");

        builder.Entity<Perro>(entity =>
        {
            entity.ToTable("Perros");
            entity.Property(perro => perro.FechaNacimiento).HasColumnType("date");
            entity.Property(perro => perro.Eliminado).HasDefaultValue(false);

            entity.HasOne(perro => perro.Tutor)
                .WithMany(tutor => tutor.Perros)
                .HasForeignKey(perro => perro.TutorId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(perro => perro.GrupoEtario)
                .WithMany()
                .HasForeignKey(perro => perro.GrupoEtarioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(perro => perro.Tamanio)
                .WithMany()
                .HasForeignKey(perro => perro.TamanioId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(perro => !perro.Eliminado && !perro.Tutor.Eliminado);
        });

        builder.Entity<PerroPatologia>(entity =>
        {
            entity.ToTable("PerrosPatologias");
            entity.HasKey(relacion => new { relacion.PerroId, relacion.PatologiaId });

            entity.HasOne(relacion => relacion.Perro)
                .WithMany(perro => perro.Patologias)
                .HasForeignKey(relacion => relacion.PerroId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(relacion => relacion.Patologia)
                .WithMany(patologia => patologia.Perros)
                .HasForeignKey(relacion => relacion.PatologiaId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(relacion => !relacion.Perro.Eliminado && !relacion.Perro.Tutor.Eliminado);
        });

        builder.Entity<Estado>().ToTable("Estados");
        builder.Entity<Estado>().HasData(
            new Estado { Id = 1, Descripcion = "Solicitado" },
            new Estado { Id = 2, Descripcion = "Confirmado" },
            new Estado { Id = 3, Descripcion = "Rechazado" },
            new Estado { Id = 4, Descripcion = "Completado" },
            new Estado { Id = 5, Descripcion = "Cancelado por tutor" },
            new Estado { Id = 6, Descripcion = "Cancelado por veterinario" });

        builder.Entity<Tratamiento>().ToTable("Tratamientos");

        builder.Entity<Servicio>().ToTable("Servicios");

        builder.Entity<CupoDiario>(entity =>
        {
            entity.ToTable("CuposDiarios", table =>
                table.HasCheckConstraint("CK_CuposDiarios_Disponibles", "\"NumeroCuposDisponibles\" >= 0"));
            entity.Property(cupo => cupo.FechaCupo).HasColumnType("date");
        });

        builder.Entity<Turno>(entity =>
        {
            entity.ToTable("Turnos");
            entity.HasOne(turno => turno.Veterinario).WithMany()
                .HasForeignKey(turno => turno.VeterinarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(turno => turno.Perro).WithMany()
                .HasForeignKey(turno => turno.PerroId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(turno => turno.CupoDiario).WithMany()
                .HasForeignKey(turno => turno.CupoDiarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(turno => turno.Estado).WithMany()
                .HasForeignKey(turno => turno.EstadoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(turno => turno.Tratamiento).WithMany()
                .HasForeignKey(turno => turno.TratamientoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(turno => !turno.Perro.Eliminado
                && !turno.Perro.Tutor.Eliminado && (turno.VeterinarioId == null || !turno.Veterinario!.Eliminado));
        });

        builder.Entity<ServicioPorTurno>(entity =>
        {
            entity.ToTable("ServiciosPorTurno");
            entity.HasKey(relacion => new { relacion.TurnoId, relacion.ServicioId });
            entity.HasOne(relacion => relacion.Turno).WithMany()
                .HasForeignKey(relacion => relacion.TurnoId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(relacion => relacion.Servicio).WithMany()
                .HasForeignKey(relacion => relacion.ServicioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(relacion => !relacion.Turno.Perro.Eliminado
                && !relacion.Turno.Perro.Tutor.Eliminado && (relacion.Turno.VeterinarioId == null || !relacion.Turno.Veterinario!.Eliminado));
        });

        builder.Entity<Veto>(entity =>
        {
            entity.ToTable("Vetos", table => table.HasCheckConstraint(
                "CK_Vetos_Fechas", "\"FechaFin\" IS NULL OR \"FechaFin\" >= \"FechaInicio\""));
            entity.Property(veto => veto.FechaInicio).HasColumnType("timestamp with time zone");
            entity.Property(veto => veto.FechaFin).HasColumnType("timestamp with time zone");
            entity.HasOne(veto => veto.Tutor).WithMany()
                .HasForeignKey(veto => veto.TutorId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(veto => veto.Veterinario).WithMany()
                .HasForeignKey(veto => veto.VeterinarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(veto => !veto.Tutor.Eliminado);
        });

        // Identificadores fijos para que las migraciones conserven los mismos roles.
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole
            {
                Id = "rol-veterinario", Name = "Veterinario",
                NormalizedName = "VETERINARIO", ConcurrencyStamp = "rol-veterinario-v1"
            },
            new IdentityRole
            {
                Id = "rol-tutor", Name = "Tutor",
                NormalizedName = "TUTOR", ConcurrencyStamp = "rol-tutor-v1"
            });
    }
}
