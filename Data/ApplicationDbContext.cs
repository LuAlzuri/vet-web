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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
