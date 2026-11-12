using System.ComponentModel.DataAnnotations;

namespace VetWeb.Dtos;

public class CargarImagenDto
{
    [Required(ErrorMessage = "Seleccioná una imagen.")]
    public IFormFile Archivo { get; set; } = null!;
}

public record ImagenDto(int PerroId, string TipoContenido, long TamanoBytes);
