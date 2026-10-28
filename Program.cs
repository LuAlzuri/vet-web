using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// La conexión se configura mediante secretos de usuario o variables de entorno.
// No se abre una conexión ni se aplican migraciones al iniciar la aplicación.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Páginas Razor del área Identity con rutas públicas en español.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Cuenta/Ingresar";
    options.AccessDeniedPath = "/Identity/Cuenta/AccesoDenegado";
});

var app = builder.Build();

// Comando explícito de preparación; nunca se ejecuta durante el inicio normal.
if (args.Contains("--crear-veterinario"))
{
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection")))
        throw new InvalidOperationException("Configurá ConnectionStrings:DefaultConnection antes de crear la cuenta inicial.");

    await using var scope = app.Services.CreateAsyncScope();
    await InicializadorIdentity.CrearVeterinarioAsync(scope.ServiceProvider, builder.Configuration);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Inicio/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Indice}/{id?}");

app.MapRazorPages();

app.Run();
