using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalsCaterSuite.Application.Common.Interfaces;
using SalsCaterSuite.Application.Features.Customers.DTOs;
using SalsCaterSuite.Domain.Entities;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ISalsCaterSuiteDbContext _context;

    public CustomersController(ISalsCaterSuiteDbContext context)
    {
        _context = context;
    }

    /// <summary>GET /api/customers — List all customers (with optional search)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = _context.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(c =>
                c.LastName.ToLower().Contains(term) ||
                c.FirstName.ToLower().Contains(term) ||
                (c.Company != null && c.Company.ToLower().Contains(term)) ||
                (c.Contact1 != null && c.Contact1.Contains(term)));
        }

        var customers = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                LastName = c.LastName,
                FirstName = c.FirstName,
                Middle = c.Middle,
                FullName = c.FullName,
                Address = c.Address,
                Contact1 = c.Contact1,
                Contact2 = c.Contact2,
                Company = c.Company,
                DateRegistered = c.DateRegistered,
                TotalBookings = c.Bookings!.Count(b => b.IsCancelled != true),
                TotalReceivables = c.Bookings!
                    .Where(b => b.IsCancelled != true)
                    .SelectMany(b => b.Payments!)
                    .Sum(p => p.AmountPaid ?? 0m)
            })
            .ToListAsync(cancellationToken);

        return Ok(customers);
    }

    /// <summary>GET /api/customers/{id} — Get customer by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == id)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId,
                LastName = c.LastName,
                FirstName = c.FirstName,
                Middle = c.Middle,
                FullName = c.FullName,
                Address = c.Address,
                Contact1 = c.Contact1,
                Contact2 = c.Contact2,
                Company = c.Company,
                DateRegistered = c.DateRegistered,
                TotalBookings = c.Bookings!.Count(b => b.IsCancelled != true),
                TotalReceivables = c.Bookings!
                    .Where(b => b.IsCancelled != true)
                    .SelectMany(b => b.Payments!)
                    .Sum(p => p.AmountPaid ?? 0m)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return customer is null ? NotFound() : Ok(customer);
    }

    /// <summary>GET /api/customers/{id}/statement — Full customer statement</summary>
    [HttpGet("{id:int}/statement")]
    public async Task<IActionResult> GetStatement(int id, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Payments)
            .Include(c => c.Bookings!)
                .ThenInclude(b => b.Package)
            .FirstOrDefaultAsync(c => c.CustomerId == id, cancellationToken);

        if (customer is null) return NotFound();

        var activeBookings = customer.Bookings!.Where(b => b.IsCancelled != true).ToList();

        var lines = activeBookings.Select(b =>
        {
            var paid = b.Payments?.Sum(p => p.AmountPaid ?? 0m) ?? 0m;
            var amount = b.PackageAmount ?? 0m;
            return new CustomerStatementLineDto
            {
                TransactionId = b.TransactionId,
                EventDate = b.StartDate,
                Occasion = b.Occasion ?? "Event",
                Amount = amount,
                Paid = paid,
                Balance = amount - paid,
                Status = b.ServeStatus == true ? "Served" : "Upcoming"
            };
        }).OrderByDescending(l => l.EventDate).ToList();

        var statement = new CustomerStatementDto
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.FullName,
            Contact = customer.Contact1 ?? customer.Contact2,
            Address = customer.Address,
            TotalBilled = lines.Sum(l => l.Amount),
            TotalPaid = lines.Sum(l => l.Paid),
            OutstandingBalance = lines.Sum(l => l.Balance),
            Transactions = lines
        };

        return Ok(statement);
    }

    /// <summary>POST /api/customers — Create new customer</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequestDto request, CancellationToken cancellationToken)
    {
        var customer = new Customer
        {
            LastName = request.LastName,
            FirstName = request.FirstName,
            Middle = request.Middle,
            Address = request.Address,
            Contact1 = request.Contact1,
            Contact2 = request.Contact2,
            Company = request.Company,
            DateRegistered = DateTime.UtcNow
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = customer.CustomerId }, new { customerId = customer.CustomerId });
    }

    /// <summary>PUT /api/customers/{id} — Update customer</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCustomerRequestDto request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == id, cancellationToken);
        if (customer is null) return NotFound();

        customer.LastName = request.LastName;
        customer.FirstName = request.FirstName;
        customer.Middle = request.Middle;
        customer.Address = request.Address;
        customer.Contact1 = request.Contact1;
        customer.Contact2 = request.Contact2;
        customer.Company = request.Company;

        await _context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}

public record CreateCustomerRequestDto(
    string LastName,
    string FirstName,
    string? Middle,
    string? Address,
    string? Contact1,
    string? Contact2,
    string? Company);
