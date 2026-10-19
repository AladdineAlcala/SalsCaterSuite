using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Calendar.Queries;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CalendarController : ControllerBase
{
    private readonly GetCalendarEventsQuery _getEvents;

    public CalendarController(GetCalendarEventsQuery getEvents)
    {
        _getEvents = getEvents;
    }

    /// <summary>GET /api/calendar/events — Get calendar events, optionally filtered by date range</summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetEvents(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        CancellationToken cancellationToken)
    {
        var result = await _getEvents.ExecuteAsync(start, end, cancellationToken);
        return Ok(result.Data);
    }
}
