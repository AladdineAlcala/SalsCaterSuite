using SalsCaterSuite.Domain.Common;

namespace SalsCaterSuite.Domain.Entities;

/// <summary>
/// A kitchen or service department (the owner's 2026-10-24 request).
/// <para>
/// It is the parent of <see cref="AddonDetail.DeptId"/> and of the legacy
/// <see cref="Menu.DeptId"/> column, which existed before this table and was orphaned until
/// this cycle. No row is seeded — a deployment populates the catalogue itself (BR-13's
/// no-business-seed rule).
/// </para>
/// </summary>
public class Department : BaseEntity
{
    public int DeptId { get; set; }

    /// <summary>The department's display name. Required; the column is <c>nvarchar(150)</c>.</summary>
    public string DeptName { get; set; } = string.Empty;

    public ICollection<Menu> Menus { get; set; } = new List<Menu>();
    public ICollection<AddonDetail> AddonDetails { get; set; } = new List<AddonDetail>();
}
