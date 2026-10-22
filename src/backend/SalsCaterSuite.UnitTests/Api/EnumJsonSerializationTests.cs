namespace SalsCaterSuite.UnitTests.Api;

using System.Text.Json;
using FluentAssertions;
using SalsCaterSuite.Application.Features.Bookings.DTOs;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.WebApi.Serialization;
using Xunit;

/// <summary>
/// Verifies the approved JSON convention (architecture/approved-api-contracts.md §8, DEC-011):
/// enums are exchanged as <b>string names</b> in both directions, while numeric enum input remains
/// accepted for backward compatibility. The tests use the exact converter configuration the API
/// registers in <c>Program.cs</c>.
/// </summary>
public class EnumJsonSerializationTests
{
    /// <summary>
    /// Builds serializer options equivalent to the MVC pipeline: web defaults (camelCase, case
    /// insensitive) plus the API's converter configuration.
    /// </summary>
    private static JsonSerializerOptions CreateApiOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        ApiJsonSerializerOptions.Configure(options);
        return options;
    }

    [Fact]
    public void Serialize_BookingDto_RendersBookingStatusAsStringName()
    {
        var dto = new BookingDto
        {
            TransactionId = 101,
            BookingNumber = "BK-20261018-00101",
            BookType = nameof(BookingType.Inside),
            Status = BookingStatus.DepositPaid
        };

        var json = JsonSerializer.Serialize(dto, CreateApiOptions());

        json.Should().Contain("\"status\":\"DepositPaid\"");
        json.Should().NotContain("\"status\":2");
    }

    [Fact]
    public void Serialize_BookingStatusEnum_RendersStringName()
    {
        var json = JsonSerializer.Serialize(BookingStatus.FullyPaid, CreateApiOptions());

        json.Should().Be("\"FullyPaid\"");
    }

    [Fact]
    public void Deserialize_BookingStatusFromStringName_ResolvesEnum()
    {
        var dto = JsonSerializer.Deserialize<BookingDto>("{\"status\":\"DepositPaid\"}", CreateApiOptions());

        dto.Should().NotBeNull();
        dto!.Status.Should().Be(BookingStatus.DepositPaid);
    }

    [Theory]
    [InlineData("\"Inside\"", BookingType.Inside)]
    [InlineData("\"Outside\"", BookingType.Outside)]
    [InlineData("1", BookingType.Outside)] // legacy numeric input must keep working
    public void Deserialize_CreateBookingRequest_AcceptsStringAndNumericEnums(string rawValue, BookingType expected)
    {
        var json = "{\"customerId\":14,\"bookType\":" + rawValue + ",\"noOfPerson\":10}";

        var request = JsonSerializer.Deserialize<CreateBookingRequestDto>(json, CreateApiOptions());

        request.Should().NotBeNull();
        request!.BookType.Should().Be(expected);
    }
}
