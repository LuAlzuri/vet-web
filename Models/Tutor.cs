using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class Tutor
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El usuario es obligatorio.")]
    [Display(Name = "Usuario")]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser Usuario { get; set; } = null!;

    public ICollection<TelefonoTutor> Telefonos { get; set; } = new List<TelefonoTutor>();

    public ICollection<Perro> Perros { get; set; } = new List<Perro>();

    [Display(Name = "Eliminado")]
    public bool Eliminado { get; set; }
}
