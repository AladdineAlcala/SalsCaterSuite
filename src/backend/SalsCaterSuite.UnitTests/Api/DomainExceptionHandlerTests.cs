namespace SalsCaterSuite.UnitTests.Api;

using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SalsCaterSuite.Domain.Exceptions;
using SalsCaterSuite.WebApi.Middleware;
using Xunit;

/// <summary>
/// Verifies the DEC-014 exception contract that the API host relies on (and which
/// DEC-026 re-confirmed): an unhandled <see cref="DomainException"/> becomes HTTP 400
/// with the <c>{ "errors": [ "..." ] }</c> shape, while every other exception is left
/// untouched so the ProblemDetails fallback can render it.
/// </summary>
public class DomainExceptionHandlerTests
{
    private static DomainExceptionHandler CreateHandler()
        => new(NullLogger<DomainExceptionHandler>.Instance);

    private static DefaultHttpContext CreateContextWithWritableBody()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    [Fact]
    public async Task TryHandleAsync_WhenDomainException_Writes400WithErrorsArrayAndReturnsTrue()
    {
        var context = CreateContextWithWritableBody();
        var handler = CreateHandler();
        const string message = "Refund amount cannot exceed the total payments received.";

        var handled = await handler.TryHandleAsync(
            context,
            new DomainException(message),
            CancellationToken.None);

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        var json = await ReadBodyAsync(context);
        using var document = JsonDocument.Parse(json);
        var errors = document.RootElement.GetProperty("errors");

        errors.ValueKind.Should().Be(JsonValueKind.Array);
        errors.GetArrayLength().Should().Be(1);
        errors[0].GetString().Should().Be(message);
    }

    [Fact]
    public async Task TryHandleAsync_WhenNonDomainException_ReturnsFalseAndLeavesResponseUntouched()
    {
        var context = CreateContextWithWritableBody();
        var handler = CreateHandler();

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("not a domain fault"),
            CancellationToken.None);

        handled.Should().BeFalse();
        // Default status is untouched (200) and nothing was written to the body.
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        context.Response.Body.Length.Should().Be(0);
        (await ReadBodyAsync(context)).Should().BeEmpty();
    }
}
