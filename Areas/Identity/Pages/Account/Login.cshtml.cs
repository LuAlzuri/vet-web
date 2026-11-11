using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VetWeb.Models;

namespace VetWeb.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;

    public LoginModel(SignInManager<ApplicationUser> signInManager, IConfiguration configuration)
    {
        _signInManager = signInManager;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; set; } = "/";

    public class InputModel
    {
        [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresá un correo electrónico válido.")]
        [Display(Name = "Correo electrónico")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        if (!ModelState.IsValid)
            return Page();

        if (string.IsNullOrWhiteSpace(_configuration.GetConnectionString("DefaultConnection")))
        {
            ModelState.AddModelError(string.Empty, "El acceso todavía no está disponible. Falta configurar la conexión a la base de datos.");
            return Page();
        }

        // Se busca por correo sin exigir que UserName coincida con Email.
        var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
        if (user is null || !user.Activo)
        {
            ModelState.AddModelError(string.Empty, "Correo electrónico o contraseña incorrectos.");
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
            return LocalRedirect(ReturnUrl);

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "La cuenta está bloqueada temporalmente por intentos fallidos. Intentá nuevamente más tarde."
            : "No se pudo iniciar sesión. Verificá el correo y la contraseña o consultá al veterinario.");
        return Page();
    }
}
