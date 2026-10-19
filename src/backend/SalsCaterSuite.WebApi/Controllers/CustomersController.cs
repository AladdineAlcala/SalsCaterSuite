using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalsCaterSuite.Application.Features.Customers.Commands;
using SalsCaterSuite.Application.Features.Customers.DTOs;
using SalsCaterSuite.Application.Features.Customers.Queries;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly GetCustomersQuery _getCustomers;
    private readonly GetCustomerByIdQuery _getCustomerById;
    private readonly GetCustomerStatementQuery _getStatement;
    private readonly CreateCustomerCommand _createCustomer;
    private readonly UpdateCustomerCommand _updateCustomer;

    public CustomersController(
        GetCustomersQuery getCustomers,
        GetCustomerByIdQuery getCustomerById,
        GetCustomerStatementQuery getStatement,
        CreateCustomerCommand createCustomer,
        UpdateCustomerCommand updateCustomer)
    {
        _getCustomers = getCustomers;
        _getCustomerById = getCustomerById;
        _getStatement = getStatement;
        _createCustomer = createCustomer;
        _updateCustomer = updateCustomer;
    }

    /// <summary>GET /api/customers — List all customers (with optional search)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await _getCustomers.ExecuteAsync(search, cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>GET /api/customers/{id} — Get customer by ID</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _getCustomerById.ExecuteAsync(id, cancellationToken);
        return result.Succeeded ? Ok(result.Data) : NotFound();
    }

    /// <summary>GET /api/customers/{id}/statement — Full customer statement</summary>
    [HttpGet("{id:int}/statement")]
    public async Task<IActionResult> GetStatement(int id, CancellationToken cancellationToken)
    {
        var result = await _getStatement.ExecuteAsync(id, cancellationToken);
        return result.Succeeded ? Ok(result.Data) : NotFound();
    }

    /// <summary>POST /api/customers — Create new customer</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _createCustomer.ExecuteAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data }, new { customerId = result.Data });
    }

    /// <summary>PUT /api/customers/{id} — Update customer</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCustomerRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _updateCustomer.ExecuteAsync(id, request, cancellationToken);
        return result.Succeeded ? NoContent() : NotFound();
    }
}
