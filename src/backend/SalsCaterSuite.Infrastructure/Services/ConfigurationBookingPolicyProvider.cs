using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalsCaterSuite.Application.Common.Interfaces;

namespace SalsCaterSuite.Infrastructure.Services;

/// <summary>
/// Binds the two deployment-configured booking figures (DEC-101, DEC-102).
/// <para>
/// <c>Bookings:Deposit:RequiredPercentage</c> and <c>Bookings:Capacity:MaximumPax</c> are
/// <b>absent by default</b>. An absent key means <b>unknown</b>, never zero: the workbench then
/// reads "Required deposit: not stated" and "Capacity: not stated", and the utilisation
/// percentage stays <c>null</c> (BR-20). The reference mockups' <c>30</c> and <c>400</c> are
/// never hardcoded.
/// </para>
/// <para>
/// This is the <b>only</b> place the two keys are read, so the quote, the register summary and
/// the workbench can never report different figures.
/// </para>
/// </summary>
public class ConfigurationBookingPolicyProvider : IBookingPolicyProvider
{
    /// <summary>The configuration path of the required deposit percentage.</summary>
    public const string RequiredDepositPercentagePath = "Bookings:Deposit:RequiredPercentage";

    /// <summary>The configuration path of the commissary's maximum daily pax.</summary>
    public const string MaximumCapacityPaxPath = "Bookings:Capacity:MaximumPax";

    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationBookingPolicyProvider> _logger;

    public ConfigurationBookingPolicyProvider(
        IConfiguration configuration,
        ILogger<ConfigurationBookingPolicyProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// The required deposit percentage, or <c>null</c> when the key is absent or malformed.
    /// </summary>
    public decimal? RequiredDepositPercentage
    {
        get
        {
            var raw = _configuration[RequiredDepositPercentagePath];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                // A malformed value must not take the workbench down: the figure is simply
                // unknown, and the rail reads "not stated".
                _logger.LogWarning(
                    "The configuration value at '{ConfigurationPath}' is not a valid decimal ('{RawValue}'); " +
                    "the required deposit percentage is treated as not stated.",
                    RequiredDepositPercentagePath,
                    raw);

                return null;
            }

            return value;
        }
    }

    /// <summary>
    /// The commissary's maximum daily pax, or <c>null</c> when the key is absent or malformed.
    /// </summary>
    public int? MaximumCapacityPax
    {
        get
        {
            var raw = _configuration[MaximumCapacityPaxPath];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                _logger.LogWarning(
                    "The configuration value at '{ConfigurationPath}' is not a valid integer ('{RawValue}'); " +
                    "the commissary capacity is treated as not stated.",
                    MaximumCapacityPaxPath,
                    raw);

                return null;
            }

            return value;
        }
    }
}
