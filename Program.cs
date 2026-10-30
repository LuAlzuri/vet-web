using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VetWeb.Data;
using VetWeb.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    options.Filters.Add<VetWeb.Controllers.ErroresCrudFilter>();
    options.ModelBindingMessageProvider.SetValueMustBeANumberAccessor(nombre => $"El campo {nombre} debe ser numérico.");
    options.ModelBindingMessageProvider.SetAttemptedValueIsInvalidAccessor((valor, nombre) => $"El valor ingresado en {nombre} no es válido.");
    options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(valor => "Este campo es obligatorio.");
});
builder.Services.AddRazorPages();
builder.Services.AddScoped<VetWeb.Services.CuentaService>();
builder.Services.AddScoped<VetWeb.Services.TurnoService>();
builder.Services.AddScoped<VetWeb.Services.CatalogosFormularioService>();
builder.Services.AddScoped<VetWeb.Services.ImagenPerroService>();

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

// Identity puede renovar la cookie al validar el sello de seguridad.
// Conservar la elección de rol para no recuperar permisos de otros perfiles.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.OnRefreshingPrincipal = context =>
    {
        var elegido = context.CurrentPrincipal?.FindFirst(VetWeb.Services.RolSesion.Claim);
        if (elegido is not null && context.NewPrincipal?.Identity is System.Security.Claims.ClaimsIdentity identidad)
            identidad.AddClaim(new System.Security.Claims.Claim(elegido.Type, elegido.Value));
        return Task.CompletedTask;
    };
});

// Páginas Razor del área Identity con rutas públicas en español.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Cuenta/Ingresar";
    options.AccessDeniedPath = "/Identity/Cuenta/AccesoDenegado";
});

var app = builder.Build();

if (args.Contains("--semilla-demo"))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("La semilla demo solo está disponible en Development.");
    await using var scope = app.Services.CreateAsyncScope();
    await SemillaDemo.CrearAsync(scope.ServiceProvider);
    return;
}


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
app.UseMiddleware<VetWeb.Middleware.CuentaActivaMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inicio}/{action=Indice}/{id?}");

app.MapRazorPages();

app.Run();
