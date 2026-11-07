using SalsCaterSuite.Domain.Common;
using SalsCaterSuite.Domain.Enums;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// One durable lifecycle-status fact for a booking (DEC-178).
/// A migration baseline row preserves the pre-migration ordinal and name. A BG-04 row records
/// one approved transition. Payment state is never written here.
/// </summary>
public class BookingStatusHistory : BaseEntity
{
    /// <summary>Baseline rows inserted by the DEC-177/DEC-178 migration, not by BG-04.</summary>
    public const string MigrationBaselineActorSource = "system/migration-baseline";

    /// <summary>The only actor/source BG-04 writes on a transition row.</summary>
    public const string Bg04ActorSource = "system/BG-04";

    public int BookingStatusHistoryId { get; private set; }

    public int TransactionId { get; private set; }

    public Booking? Booking { get; private set; }

    /// <summary>The status this row records. Baseline rows keep the original legacy ordinal.</summary>
    public int StatusOrdinal { get; private set; }

    /// <summary>The status name this row records, including legacy names the enum no longer has.</summary>
    public string StatusName { get; private set; } = string.Empty;

    public int? PreviousStatusOrdinal { get; private set; }

    public string? PreviousStatusName { get; private set; }

    public string ActorSource { get; private set; } = string.Empty;

    /// <summary>When the status became effective. For BG-04 this is the threshold instant, in UTC.</summary>
    public DateTime EffectiveAtUtc { get; private set; }

    /// <summary>When this row was written, in UTC.</summary>
    public DateTime RecordedAtUtc { get; private set; }

    private BookingStatusHistory()
    {
    }

    public static BookingStatusHistory ForTransition(
        int transactionId,
        BookingStatus from,
        BookingStatus to,
        DateTime effectiveAtUtc,
        DateTime recordedAtUtc)
    {
        return new BookingStatusHistory
        {
            TransactionId = transactionId,
            PreviousStatusOrdinal = (int)from,
            PreviousStatusName = from.ToString(),
            StatusOrdinal = (int)to,
            StatusName = to.ToString(),
            ActorSource = Bg04ActorSource,
            EffectiveAtUtc = effectiveAtUtc,
            RecordedAtUtc = recordedAtUtc,
            CreatedDate = recordedAtUtc,
            CreatedByUser = Bg04ActorSource
        };
    }
}
