using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace VetWeb.Controllers;

public class ErroresCrudFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is UnauthorizedAccessException)
        {
            context.Result = new ForbidResult();
            context.ExceptionHandled = true;
            return;
        }
        var mensaje = context.Exception switch
        {
            Npgsql.PostgresException error when error.SqlState == "40001" || error.SqlState == "40P01" => "Otra operación modificó la disponibilidad. Actualizá la página e intentá nuevamente.",
            InvalidOperationException error => error.Message,
            DbUpdateConcurrencyException => "El registro cambió durante la operación. Actualizá la página e intentá nuevamente.",
            DbUpdateException => "No se pudo guardar. Revisá si el registro está duplicado o tiene datos relacionados.",
            _ => null
        };
        if (mensaje is null) return;
        context.Result = new BadRequestObjectResult(new { mensaje });
        context.ExceptionHandled = true;
    }
}
