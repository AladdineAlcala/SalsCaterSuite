namespace SalsCaterSuite.IntegrationTests.Infrastructure;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// A <b>test-only</b> controller, registered through the integration host's
/// <c>ConfigureTestServices</c> hook and a dedicated <c>ApplicationPart</c> (specification §7).
/// <para>
/// It adds <b>no production route</b>: the controller lives in the integration-test assembly and
/// is only ever discovered by the test host.
/// </para>
/// <para>
/// It exists for two reasons:
/// <list type="bullet">
/// <item><b>AC-OPS-11</b> — it raises a <b>second log line inside a request</b> through the ordinary
/// <c>ILogger&lt;T&gt;</c> an application uses, so the correlation id can be read off that line and
/// compared with the request event's. Nothing in the production code raises an application log
/// line inside a request, so this is the only way to prove the scoping end to end.</item>
/// <item><b>Specification §7</b> — it raises an unhandled fault on demand, so the failure event can
/// be observed deterministically.</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
[Route("api/test-operational-logging")]
public class TestOperationalLoggingController : ControllerBase
{
    private readonly ILogger<TestOperationalLoggingController> _logger;

    public TestOperationalLoggingController(ILogger<TestOperationalLoggingController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Raises a second log line inside the request and returns. The line must carry the same
    /// <c>TraceId</c> as the request event (AC-OPS-11).
    /// </summary>
    [HttpGet("correlation")]
    public IActionResult Correlation()
    {
        _logger.LogWarning("A second line raised while the request was handled.");

        return Ok(new { raised = true });
    }

    /// <summary>Raises an unhandled fault (AC-OPS-02).</summary>
    [HttpGet("fault")]
    public IActionResult Fault() =>
        throw new InvalidOperationException("A test-only unhandled fault.");
}
