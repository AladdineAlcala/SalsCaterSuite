namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Features.Menus.Models;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.UnitTests.Common;
using Xunit;

/// <summary>
/// Infrastructure-layer tests for the menu repository (DEC-037). The EF-specific query
/// behaviour (projections, ordering, existence check, delete) is proven here against EF
/// Core; the Application tests use the hand-written fake and cannot cover this.
/// </summary>
public class MenuRepositoryTests : IDisposable
{
    private readonly SalsCaterSuiteDbContext _context;
    private readonly MenuRepository _repository;

    public MenuRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new MenuRepository(_context);
    }

    private async Task SeedCourseAsync(int courseId, string courseName)
    {
        await _context.CourseCategories.AddAsync(new CourseCategory { CourseId = courseId, CourseName = courseName });
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task AddAsync_And_SaveChangesAsync_ShouldPersistMenu()
    {
        // Arrange
        var menu = new Menu { MenuId = "M001", MenuName = "Beef Stroganoff", Price = 350m, FoodCost = 105m };

        // Act
        await _repository.AddAsync(menu);
        await _repository.SaveChangesAsync();

        // Assert
        var persisted = await _context.Menus.FindAsync("M001");
        persisted.Should().NotBeNull();
        persisted!.MenuName.Should().Be("Beef Stroganoff");
    }

    [Fact]
    public async Task GetByIdAsync_WhenMenuDoesNotExist_ShouldReturnNull()
    {
        (await _repository.GetByIdAsync("NOPE")).Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReflectPersistedRows()
    {
        await _context.Menus.AddAsync(new Menu { MenuId = "M001", MenuName = "Existing" });
        await _context.SaveChangesAsync();

        (await _repository.ExistsAsync("M001")).Should().BeTrue();
        (await _repository.ExistsAsync("M999")).Should().BeFalse();
    }

    [Fact]
    public async Task GetOrderedByNameAsync_ShouldOrderByMenuName()
    {
        await _context.Menus.AddRangeAsync(
            new Menu { MenuId = "M2", MenuName = "Zesty Chicken" },
            new Menu { MenuId = "M1", MenuName = "Apple Glazed Pork" });
        await _context.SaveChangesAsync();

        var result = await _repository.GetOrderedByNameAsync();

        result.Should().HaveCount(2);
        result[0].MenuName.Should().Be("Apple Glazed Pork");
        result[1].MenuName.Should().Be("Zesty Chicken");
    }

    [Fact]
    public async Task GetProjectionListAsync_ShouldIncludeCourseNameAndFilterByCourse()
    {
        // Arrange
        await SeedCourseAsync(1, "Main Course");
        await SeedCourseAsync(2, "Dessert");
        await _context.Menus.AddRangeAsync(
            new Menu { MenuId = "M1", CourseId = 1, MenuName = "Beef Steak", Price = 400m, FoodCost = 120m },
            new Menu { MenuId = "M2", CourseId = 2, MenuName = "Leche Flan", Price = 120m, FoodCost = 40m });
        await _context.SaveChangesAsync();

        // Act — unfiltered
        var all = await _repository.GetProjectionListAsync(null);
        // Act — filtered by course 2
        var desserts = await _repository.GetProjectionListAsync(2);

        // Assert
        all.Should().HaveCount(2);
        all.Single(m => m.MenuId == "M1").CourseName.Should().Be("Main Course");
        desserts.Should().ContainSingle().Which.MenuId.Should().Be("M2");
    }

    [Fact]
    public async Task GetProjectionByIdAsync_ShouldReturnProjectionWithCourseName()
    {
        // Arrange
        await SeedCourseAsync(1, "Main Course");
        await _context.Menus.AddAsync(new Menu { MenuId = "M1", CourseId = 1, MenuName = "Beef Steak", Price = 400m, FoodCost = 120m });
        await _context.SaveChangesAsync();

        // Act
        var projection = await _repository.GetProjectionByIdAsync("M1");

        // Assert
        projection.Should().NotBeNull();
        projection!.MenuId.Should().Be("M1");
        projection.CourseId.Should().Be(1);
        projection.CourseName.Should().Be("Main Course");
    }

    [Fact]
    public async Task GetCourseProjectionsAsync_ShouldOrderByCourseId()
    {
        // Arrange
        await SeedCourseAsync(2, "Dessert");
        await SeedCourseAsync(1, "Main Course");

        // Act
        var courses = await _repository.GetCourseProjectionsAsync();

        // Assert
        courses.Should().HaveCount(2);
        courses[0].CourseId.Should().Be(1);
        courses[0].CourseName.Should().Be("Main Course");
    }

    [Fact]
    public async Task Remove_ShouldDeletePersistedMenu()
    {
        // Arrange
        await _context.Menus.AddAsync(new Menu { MenuId = "M001", MenuName = "To Delete" });
        await _context.SaveChangesAsync();
        var menu = await _repository.GetByIdAsync("M001");

        // Act
        _repository.Remove(menu!);
        await _repository.SaveChangesAsync();

        // Assert
        (await _context.Menus.FindAsync("M001")).Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // The soft-delete exclusion of the legacy reads (DEC-063)
    // ═══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task The_legacy_reads_should_exclude_the_soft_deleted_dishes()
    {
        // Arrange
        await SeedCourseAsync(1, "Main Course");
        await _context.Menus.AddRangeAsync(
            new Menu { MenuId = "LIVE", CourseId = 1, MenuName = "Live Dish", Price = 350m, FoodCost = 105m },
            new Menu { MenuId = "DEAD", CourseId = 1, MenuName = "Deleted Dish", Price = 300m, FoodCost = 90m, IsDeleted = true });
        await _context.SaveChangesAsync();

        // Act & Assert — the list projection.
        var list = await _repository.GetProjectionListAsync(null);
        list.Should().ContainSingle().Which.MenuId.Should().Be("LIVE");

        // The single projection — a deleted dish is not found.
        (await _repository.GetProjectionByIdAsync("DEAD")).Should().BeNull();
        (await _repository.GetProjectionByIdAsync("LIVE")).Should().NotBeNull();

        // The by-id read — the write commands and the costing endpoint agree.
        (await _repository.GetByIdAsync("DEAD")).Should().BeNull();
        (await _repository.GetByIdAsync("LIVE")).Should().NotBeNull();

        // The costing-all read.
        var ordered = await _repository.GetOrderedByNameAsync();
        ordered.Should().ContainSingle().Which.MenuId.Should().Be("LIVE");
    }

    [Fact]
    public async Task ExistsAsync_should_still_see_a_soft_deleted_dish_because_the_code_is_the_key()
    {
        // Arrange
        await _context.Menus.AddAsync(new Menu { MenuId = "DEAD", MenuName = "Deleted Dish", IsDeleted = true });
        await _context.SaveChangesAsync();

        // Act & Assert — a new dish with the same code would be a key collision (a 500), so
        // the duplicate check must still report the code as taken (a 409).
        (await _repository.ExistsAsync("DEAD")).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // The Menu & Food Costing Master read (spec §6.1, §10.1 M1–M8)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>The fixed target the margin-alert assertions are anchored on.</summary>
    private const decimal TargetFoodCostPercentage = 32m;

    private static MenuMasterFilter Filter(
        string? search = null,
        int? courseId = null,
        MenuMasterStatus status = MenuMasterStatus.All,
        MenuMasterSort sort = MenuMasterSort.FoodCostAsc,
        int page = 1,
        int pageSize = 12) => new()
    {
        Search = search,
        CourseId = courseId,
        Status = status,
        Sort = sort,
        TargetFoodCostPercentage = TargetFoodCostPercentage,
        Page = page,
        PageSize = pageSize
    };

    private async Task SeedMasterCatalogueAsync()
    {
        await _context.CourseCategories.AddRangeAsync(
            new CourseCategory { CourseId = 1, CourseName = "Appetizer" },
            new CourseCategory { CourseId = 2, CourseName = "Main Course" });
        await _context.SaveChangesAsync();

        await _context.Menus.AddRangeAsync(
            // 30 % food cost
            new Menu { MenuId = "M1", CourseId = 2, MenuName = "Roast Beef", Price = 350m, FoodCost = 105m },
            // 30 % food cost
            new Menu { MenuId = "M2", CourseId = 2, MenuName = "Beef Steak", Price = 300m, FoodCost = 90m },
            // 30 % food cost
            new Menu { MenuId = "M3", CourseId = 1, MenuName = "Lumpia", Price = 200m, FoodCost = 60m },
            // uncosted
            new Menu { MenuId = "M4", CourseId = 1, MenuName = "Leche Flan", Price = 150m, FoodCost = null },
            // 40 % food cost — a margin alert
            new Menu { MenuId = "M5", CourseId = 2, MenuName = "Bespoke Platter", Price = 100m, FoodCost = 40m },
            // soft-deleted — excluded everywhere
            new Menu { MenuId = "M6", CourseId = 2, MenuName = "Deleted Dish", Price = 100m, FoodCost = 10m, IsDeleted = true });

        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task The_master_data_set_should_exclude_the_soft_deleted_dishes()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act
        var data = await _repository.GetMenuMasterDataSetAsync(Filter());

        // Assert
        data.Rows.Should().HaveCount(5);
        data.Rows.Should().NotContain(r => r.MenuId == "M6");
        data.Aggregate.TotalCount.Should().Be(5);
        data.Aggregate.CostedCount.Should().Be(4);
    }

    [Fact]
    public async Task The_master_data_set_should_measure_the_margin_population()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act
        var data = await _repository.GetMenuMasterDataSetAsync(Filter());

        // Assert — the margin population is the four costed dishes that carry a price.
        data.Aggregate.MarginPopulationCount.Should().Be(4);
        data.Aggregate.RevenueAmount.Should().Be(950m);
        data.Aggregate.FoodCostAmount.Should().Be(295m);
    }

    [Fact]
    public async Task The_master_data_set_should_count_the_margin_alerts()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act
        var data = await _repository.GetMenuMasterDataSetAsync(Filter());

        // Assert — only M5 (40 %) is above the 32 % target.
        data.MarginAlertCount.Should().Be(1);
    }

    [Fact]
    public async Task The_master_data_set_should_group_the_margin_figures_by_course()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act
        var data = await _repository.GetMenuMasterDataSetAsync(Filter());

        // Assert
        var mainCourse = data.CourseMargins.Single(c => c.CourseId == 2);
        mainCourse.RevenueAmount.Should().Be(750m);
        mainCourse.FoodCostAmount.Should().Be(235m);
        mainCourse.DishCount.Should().Be(3);

        var appetizer = data.CourseMargins.Single(c => c.CourseId == 1);
        appetizer.RevenueAmount.Should().Be(200m);
        appetizer.FoodCostAmount.Should().Be(60m);
        appetizer.DishCount.Should().Be(1);
    }

    [Fact]
    public async Task The_master_data_set_should_report_the_catalogue_course_counts()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act — a course scope is selected, but the tab counts describe the catalogue.
        var data = await _repository.GetMenuMasterDataSetAsync(Filter(courseId: 1));

        // Assert
        data.Courses.Items.Should().HaveCount(2);
        data.Courses.Items.Single(c => c.CourseId == 1).DishCount.Should().Be(2, "M3 and M4");
        data.Courses.Items.Single(c => c.CourseId == 2).DishCount.Should().Be(3, "M1, M2 and M5");
        data.Courses.Total.Should().Be(5, "the tab strip's scope is everything except the course filter");
        data.Aggregate.TotalCount.Should().Be(2, "the aggregate follows the active scope");
    }

    [Fact]
    public async Task The_master_data_set_should_search_the_dish_code_the_name_and_the_note()
    {
        // Arrange
        await _context.CourseCategories.AddAsync(new CourseCategory { CourseId = 1, CourseName = "Appetizer" });
        await _context.Menus.AddRangeAsync(
            new Menu { MenuId = "DS-104", CourseId = 1, MenuName = "Prime Roast Ribeye", Price = 420m, FoodCost = 138.60m, Note = "Signature" },
            new Menu { MenuId = "DS-105", CourseId = 1, MenuName = "Lumpia", Price = 200m, FoodCost = 60m, Note = "Crispy spring roll" });
        await _context.SaveChangesAsync();

        // Act
        var byCode = await _repository.GetMenuMasterDataSetAsync(Filter(search: "ds-104"));
        var byName = await _repository.GetMenuMasterDataSetAsync(Filter(search: "RIBEYE"));
        var byNote = await _repository.GetMenuMasterDataSetAsync(Filter(search: "spring roll"));

        // Assert
        byCode.Rows.Should().ContainSingle().Which.MenuId.Should().Be("DS-104");
        byName.Rows.Should().ContainSingle().Which.MenuId.Should().Be("DS-104");
        byNote.Rows.Should().ContainSingle().Which.MenuId.Should().Be("DS-105");
    }

    [Fact]
    public async Task The_master_data_set_should_apply_the_status_filter()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act
        var costed = await _repository.GetMenuMasterDataSetAsync(Filter(status: MenuMasterStatus.Costed));
        var uncosted = await _repository.GetMenuMasterDataSetAsync(Filter(status: MenuMasterStatus.Uncosted));
        var alerts = await _repository.GetMenuMasterDataSetAsync(Filter(status: MenuMasterStatus.MarginAlert));

        // Assert
        costed.Rows.Should().HaveCount(4);
        costed.Rows.Should().NotContain(r => r.MenuId == "M4");
        uncosted.Rows.Should().ContainSingle().Which.MenuId.Should().Be("M4");
        alerts.Rows.Should().ContainSingle().Which.MenuId.Should().Be("M5");
    }

    [Fact]
    public async Task The_master_rows_should_be_ordered_and_paged()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act
        var data = await _repository.GetMenuMasterDataSetAsync(Filter(sort: MenuMasterSort.FoodCostAsc, page: 1, pageSize: 2));
        var secondPage = await _repository.GetMenuMasterDataSetAsync(Filter(sort: MenuMasterSort.FoodCostAsc, page: 2, pageSize: 2));

        // Assert — M4 (no food cost → 0 %) first, then the 30 % dishes, then M5 (40 %).
        data.Rows.Select(r => r.MenuId).Should().Equal("M4", "M1");
        secondPage.Rows.Select(r => r.MenuId).Should().Equal("M2", "M3");
    }

    [Fact]
    public async Task The_unpaged_rows_should_return_the_whole_filtered_set()
    {
        // Arrange
        await SeedMasterCatalogueAsync();

        // Act
        var rows = await _repository.GetMenuMasterRowsAsync(Filter());

        // Assert
        rows.Should().HaveCount(5);
        rows.Should().NotContain(r => r.MenuId == "M6");
    }

    [Fact]
    public async Task CourseExistsAsync_ShouldReflectTheSeededCourseCategories()
    {
        // Arrange
        await _context.CourseCategories.AddAsync(new CourseCategory { CourseId = 7, CourseName = "Soup" });
        await _context.SaveChangesAsync();

        // Act & Assert
        (await _repository.CourseExistsAsync(7)).Should().BeTrue();
        (await _repository.CourseExistsAsync(99)).Should().BeFalse();
    }

    /// <summary>
    /// The database-side half of the master read must be a single translatable statement — the
    /// filters, the ordering and the aggregation are applied by SQL Server, never client-side.
    /// <para>
    /// The per-dish rounding is <c>Math.Round(…, 2)</c>, which SQL Server cannot translate, so
    /// the derived figures are composed in the Application layer (DEC-039's accepted deviation).
    /// The probe asserts that the <b>arithmetic</b> the SQL does use is translatable.
    /// </para>
    /// </summary>
    [Fact]
    public void TheMenuMasterProjection_ShouldBeTranslatableBySqlServer()
    {
        var options = new DbContextOptionsBuilder<SalsCaterSuiteDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SalsCaterSuite_QueryTranslationProbe;Trusted_Connection=True;MultipleActiveResultSets=true")
            .Options;

        using var context = new SalsCaterSuiteDbContext(options);

        var term = "beef".ToLower();
        var target = TargetFoodCostPercentage;

        // The search filter, the status filter, the margin-alert filter, the ordering and the
        // row projection — the very shapes the repository uses.
        var rows = context.Menus
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .Where(m =>
                m.MenuId.ToLower().Contains(term) ||
                m.MenuName.ToLower().Contains(term) ||
                (m.Note != null && m.Note.ToLower().Contains(term)))
            .Where(m => m.FoodCost == null || m.FoodCost <= 0m)
            .OrderBy(m => (m.FoodCost ?? 0m) / (m.Price > 0m ? (m.Price ?? 0m) : 1m) * 100m)
            .ThenBy(m => m.MenuId)
            .Select(m => new
            {
                m.MenuId,
                m.MenuName,
                m.CourseId,
                CourseName = m.CourseCategory != null ? m.CourseCategory.CourseName : null,
                m.Note,
                m.Image,
                m.Price,
                m.FoodCost
            });

        var rowsSql = rows.ToQueryString();
        rowsSql.Should().NotBeNullOrWhiteSpace();
        rowsSql.Should().Contain("ORDER BY");
        rowsSql.Should().Contain("is_deleted");

        // The margin-alert predicate, composed exactly as the repository composes it.
        Expression<Func<Menu, decimal>> key =
            m => (m.FoodCost ?? 0m) / (m.Price > 0m ? (m.Price ?? 0m) : 1m) * 100m;
        var alertPredicate = Expression.Lambda<Func<Menu, bool>>(
            Expression.GreaterThan(key.Body, Expression.Constant(target)),
            key.Parameters);

        var alerts = context.Menus
            .AsNoTracking()
            .Where(m => !m.IsDeleted)
            .Where(alertPredicate);

        var alertsSql = alerts.ToQueryString();
        alertsSql.Should().NotBeNullOrWhiteSpace();
        alertsSql.Should().Contain("WHERE");

        // The aggregates: the counts, the sums and the per-course grouping.
        var aggregates = context.Menus
            .AsNoTracking()
            .Where(m => !m.IsDeleted && m.Price > 0m && m.FoodCost > 0m)
            .GroupBy(m => m.CourseId)
            .Select(g => new
            {
                CourseId = g.Key,
                RevenueAmount = g.Sum(m => m.Price) ?? 0m,
                FoodCostAmount = g.Sum(m => m.FoodCost) ?? 0m,
                DishCount = g.Count()
            });

        var aggregatesSql = aggregates.ToQueryString();
        aggregatesSql.Should().NotBeNullOrWhiteSpace();
        aggregatesSql.Should().Contain("SUM");
        aggregatesSql.Should().Contain("GROUP BY");
    }

    public void Dispose()
    {
        TestDbContextFactory.Destroy(_context);
    }
}
