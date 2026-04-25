using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ExceptionHandling;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = string.Join("; ", ex.Errors.Select(x => x.ErrorMessage))
            };
            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Unexpected error",
                Detail = "An unexpected error occurred."
            };
            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}

