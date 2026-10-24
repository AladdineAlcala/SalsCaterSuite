using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalsCaterSuite.Application.Common.Interfaces;

namespace SalsCaterSuite.Infrastructure.Services;

/// <summary>
/// Binds the deployment-configured booking figures (DEC-101, DEC-102, DEC-128, DEC-143).
/// <para>
/// <c>Bookings:Deposit:RequiredPercentage</c>, <c>Bookings:Capacity:MaximumPax</c>,
/// <c>Bookings:MenuSelection:LockDaysBeforeEvent</c> and <c>Bookings:Tax:VatPercentage</c> are
/// <b>absent by default</b>. An absent key means <b>unknown</b>, never zero: the workbench then
/// reads "Required deposit: not stated" and "Capacity: not stated", the utilisation percentage
/// stays <c>null</c>, the menu-change window's <b>days</b> are <c>null</c> (its lock date is the
/// event date) and the VAT disclosure is <c>null</c> (BR-20, BR-36, BR-42). The reference
/// mockups' <c>30</c>, <c>400</c>, <c>7</c> and <c>12</c> are never hardcoded.
/// </para>
/// <para>
/// This is the <b>only</b> place the four keys are read, so the quote, the register, the booking
/// selection read and the workbench can never report different figures.
/// </para>
/// </summary>
public class ConfigurationBookingPolicyProvider : IBookingPolicyProvider
{
    /// <summary>The configuration path of the required deposit percentage.</summary>
    public const string RequiredDepositPercentagePath = "Bookings:Deposit:RequiredPercentage";

    /// <summary>The configuration path of the commissary's maximum daily pax.</summary>
    public const string MaximumCapacityPaxPath = "Bookings:Capacity:MaximumPax";

    /// <summary>The configuration path of the menu-change window's lead time (DEC-128).</summary>
    public const string MenuSelectionLockDaysBeforeEventPath = "Bookings:MenuSelection:LockDaysBeforeEvent";

    /// <summary>The configuration path of the VAT-inclusive rate (DEC-143).</summary>
    public const string VatPercentagePath = "Bookings:Tax:VatPercentage";

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

    /// <summary>
    /// The menu-change window's lead time in days, or <c>null</c> when the key is absent or
    /// malformed. The window's <b>lock date</b> is unaffected: it falls back to the event date.
    /// </summary>
    public int? MenuSelectionLockDaysBeforeEvent
    {
        get
        {
            var raw = _configuration[MenuSelectionLockDaysBeforeEventPath];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                _logger.LogWarning(
                    "The configuration value at '{ConfigurationPath}' is not a valid integer ('{RawValue}'); " +
                    "the menu-change lead time is treated as not stated and the lock date becomes the event date.",
                    MenuSelectionLockDaysBeforeEventPath,
                    raw);

                return null;
            }

            return value;
        }
    }

    /// <summary>
    /// The VAT-inclusive rate, or <c>null</c> when the key is absent or malformed.
    /// </summary>
    public decimal? VatPercentage
    {
        get
        {
            var raw = _configuration[VatPercentagePath];

            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                _logger.LogWarning(
                    "The configuration value at '{ConfigurationPath}' is not a valid decimal ('{RawValue}'); " +
                    "the VAT disclosure is treated as not stated.",
                    VatPercentagePath,
                    raw);

                return null;
            }

            return value;
        }
    }
}