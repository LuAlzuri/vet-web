using System.ComponentModel.DataAnnotations;
namespace VetWeb.Services;
internal static class ValidacionDto
{
    public static void Comprobar(object dto)
    {
        var errores = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), errores, true))
            throw new InvalidOperationException(errores[0].ErrorMessage);
    }
}
