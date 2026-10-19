using Microsoft.AspNetCore.Diagnostics;
using SalsCaterSuite.Domain.Exceptions;

namespace SalsCaterSuite.WebApi.Middleware;

/// <summary>
/// Maps any unhandled <see cref="DomainException"/> to HTTP 400 with the same
/// <c>{ "errors": [ "..." ] }</c> shape used by the controllers, so a domain
/// rule violation can never surface as an opaque 500 (DEC-014). All other
/// exceptions fall through to the default ASP.NET Core behaviour.
/// </summary>
public class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainException domainException)
        {
            return false;
        }

        _logger.LogWarning(domainException, "Domain rule violation: {Message}", domainException.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await httpContext.Response.WriteAsJsonAsync(
            new { errors = new[] { domainException.Message } },
            cancellationToken);

        return true;
    }
}
