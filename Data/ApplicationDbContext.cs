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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
