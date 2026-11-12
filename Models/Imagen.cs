using System.ComponentModel.DataAnnotations;

namespace VetWeb.Models;

public class Imagen
{
    public int Id { get; set; }
    public int PerroId { get; set; }
    public Perro Perro { get; set; } = null!;

    [MaxLength(40)]
    public string NombreArchivo { get; set; } = string.Empty;

    [MaxLength(30)]
    public string TipoContenido { get; set; } = string.Empty;

    public long TamanoBytes { get; set; }
}
