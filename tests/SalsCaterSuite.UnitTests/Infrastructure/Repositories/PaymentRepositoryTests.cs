namespace SalsCaterSuite.UnitTests.Infrastructure.Repositories;

using FluentAssertions;
using SalsCaterSuite.Domain.Entities;
using SalsCaterSuite.Domain.Enums;
using SalsCaterSuite.Infrastructure.Persistence;
using SalsCaterSuite.Infrastructure.Persistence.Repositories;
using SalsCaterSuite.UnitTests.Common;
using Xunit;

/// <summary>
/// Infrastructure-layer tests for the payment repository (DEC-037).
/// <para>
/// This is where EF-specific query behaviour lives: the Application use-case tests use
/// hand-written fakes and therefore cannot prove that <c>CountCreatedOnDateAsync</c>'s
/// predicate translates and filters the way the original inline LINQ query did. That
/// assertion moved here (EF InMemory) rather than being dropped.
/// </para>
/// </summary>
public class PaymentRepositoryTests : IDisposable
{
    private readonly SalsCaterSuiteDbContext _context;
    private readonly PaymentRepository _repository;

    public PaymentRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new PaymentRepository(_context);
    }

    private static Payment PaymentOn(string paymentNo, DateTime? createdDate, decimal amount = 1000m)
        => new()
        {
            PaymentNo = paymentNo,
            TransactionId = 1,
            AmountPaid = amount,
            Particular = "Deposit",
            DateOfPayment = DateTime.UtcNow,
            PaymentMeans = PaymentMeans.Cash,
            CreatedDate = createdDate
        };

    [Fact]
    public async Task AddAsync_And_SaveChangesAsync_ShouldPersistPayment()
    {
        // Arrange
        var payment = PaymentOn("OR-20261019-0001", DateTime.UtcNow, 5000m);

        // Act
        await _repository.AddAsync(payment);
        await _repository.SaveChangesAsync();

        // Assert
        var persisted = _context.Payments.Single();
        persisted.PaymentNo.Should().Be("OR-20261019-0001");
        persisted.AmountPaid.Should().Be(5000m);
    }

    [Fact]
    public async Task CountCreatedOnDateAsync_ShouldCountOnlyThatCalendarDay()
    {
        // Arrange
        var today = DateTime.UtcNow;
        await _context.Payments.AddRangeAsync(
            PaymentOn("OR-A", today),
            PaymentOn("OR-B", today.Date.AddHours(1)),          // same day, different time
            PaymentOn("OR-C", today.AddDays(-1)),               // previous day
            PaymentOn("OR-D", null));                           // no CreatedDate
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.CountCreatedOnDateAsync(today);

        // Assert — mirrors the original predicate: CreatedDate must exist and match the day.
        count.Should().Be(2);
    }

    [Fact]
    public async Task CountCreatedOnDateAsync_WhenNoPaymentsExist_ShouldReturnZero()
    {
        // Act
        var count = await _repository.CountCreatedOnDateAsync(DateTime.UtcNow);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task Remove_ShouldNotDeleteWhenThePaymentWasOnlyStaged()
    {
        // Arrange — the domain-rejection path stages then removes a payment.
        var payment = PaymentOn("OR-STAGED", DateTime.UtcNow);
        await _repository.AddAsync(payment);

        // Act
        _repository.Remove(payment);
        await _repository.SaveChangesAsync();

        // Assert
        _context.Payments.Should().BeEmpty();
    }

    public void Dispose()
    {
        TestDbContextFactory.Destroy(_context);
    }
}
