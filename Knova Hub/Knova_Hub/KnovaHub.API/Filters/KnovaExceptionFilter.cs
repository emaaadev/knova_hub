using KnovaHub.InfrastructureLayer.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KnovaHub.API.Filters;

/// <summary>
/// Convierte las excepciones de negocio en respuestas HTTP con { message } (y { field } en duplicados).
/// </summary>
public class KnovaExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        IActionResult? result = context.Exception switch
        {
            NotFoundException ex => new NotFoundObjectResult(new { message = ex.Message }),
            BusinessRuleException ex => new ConflictObjectResult(new { message = ex.Message }),
            DuplicateEntityException ex => new ConflictObjectResult(new { field = ex.Field, message = ex.Message }),
            DataValidationException ex => new BadRequestObjectResult(new { message = ex.Message }),
            _ => null
        };

        if (result is not null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}
