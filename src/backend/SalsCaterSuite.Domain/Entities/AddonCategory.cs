using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// The add-on master's category level (REQ-BKG-07, DEC-142).
/// <para>
/// It is <b>read-only</b> in this cycle (RSK-066): no endpoint creates or edits a category.
/// No row is seeded (BR-13) — the master ships empty, so the add-on picker is empty until the
/// catalogue is populated.
/// </para>
/// </summary>
public class AddonCategory : BaseEntity
{
    public int AddonCategoryId { get; set; }

    public string AddonCategoryName { get; set; } = string.Empty;
}
