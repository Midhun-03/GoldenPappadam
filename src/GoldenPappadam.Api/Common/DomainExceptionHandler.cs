using Microsoft.AspNetCore.Diagnostics;

namespace GoldenPappadam.Api.Common;

/// <summary>
/// Turns business-rule failures into a plain problem response, so controllers and services
/// do not have to pass error objects around.
/// </summary>
public class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        var status = exception is NotFoundException
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status400BadRequest;

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = status,
                Title = status == StatusCodes.Status404NotFound ? "Not found" : "Request cannot be completed",
                Detail = domainException.Message
            }
        });
    }
}
