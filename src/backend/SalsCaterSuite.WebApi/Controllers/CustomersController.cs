using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using SalsCaterSuite.Application.Features.Customers;
using SalsCaterSuite.Application.Features.Customers.Commands;
using SalsCaterSuite.Application.Features.Customers.DTOs;
using SalsCaterSuite.Application.Features.Customers.Queries;
using SalsCaterSuite.Infrastructure.Documents;
using SalsCaterSuite.Infrastructure.Exports;

namespace SalsCaterSuite.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly GetCustomersQuery _getCustomers;
    private readonly GetCustomerByIdQuery _getCustomerById;
    private readonly GetCustomerStatementQuery _getStatement;
    private readonly GetCustomerDirectoryQuery _getDirectory;
    private readonly CreateCustomerCommand _createCustomer;
    private readonly UpdateCustomerCommand _updateCustomer;
    private readonly CustomerDirectoryExport _directoryExport;

    public CustomersController(
        GetCustomersQuery getCustomers,
        GetCustomerByIdQuery getCustomerById,
        GetCustomerStatementQuery getStatement,
        GetCustomerDirectoryQuery getDirectory,
        CreateCustomerCommand createCustomer,
        UpdateCustomerCommand updateCustomer,
        CustomerDirectoryExport directoryExport)
    {
        _getCustomers = getCustomers;
        _getCustomerById = getCustomerById;
        _getStatement = getStatement;
        _getDirectory = getDirectory;
        _createCustomer = createCustomer;
        _updateCustomer = updateCustomer;
        _directoryExport = directoryExport;
    }

    /// <summary>GET /api/customers — List all customers (with optional search)</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await _getCustomers.ExecuteAsync(search, cancellationToken);
        return Ok(result.Data);
    }

    /// <summary>
    /// GET /api/customers/directory — the paged customer directory with its AR ledger state
    /// (spec §5.1).
    /// </summary>
    /// <param name="search">Case-insensitive substring over last name, first name, company and contact 1.</param>
    /// <param name="customerType"><c>Corporate</c> or <c>Private</c>; any other value is ignored.</param>
    /// <param name="arState"><c>Clean</c>, <c>Open</c>, <c>Overdue30</c> or <c>Overdue60</c>; any other value is ignored.</param>
    /// <param name="page">The 1-based page number; values below 1 are clamped to 1.</param>
    /// <param name="pageSize">The page size; clamped to 1…100.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpGet("directory")]
    [ProducesResponseType(typeof(CustomerDirectoryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDirectory(
        [FromQuery] string? search,
        [FromQuery] string? customerType,
        [FromQuery] string? arState,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = GetCustomerDirectoryQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await _getDirectory.ExecuteAsync(
            search, customerType, arState, page, pageSize, cancellationToken);

        return Ok(result.Data);
    }

    /// <summary>
    /// GET /api/customers/directory/export — the current filtered result set as XLSX
    /// (spec §5.5). The export is never paged.
    /// </summary>
    [HttpGet("directory/export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportDirectory(
        [FromQuery] string? search,
        [FromQuery] string? customerType,
        [FromQuery] string? arState,
        CancellationToken cancellationToken)
    {
        var result = await _getDirectory.ExecuteUnpagedAsync(search, customerType, arState, cancellationToken);

        var bytes = _directoryExport.Generate(result.Data!);

        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"CustomerDirectory-{DateTime.Now:yyyyMMdd}.xlsx");
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

    /// <summary>
    /// GET /api/customers/{id}/soa — the Statement of Account as a PDF (spec §5.4).
    /// </summary>
    [HttpGet("{id:int}/soa")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatementOfAccount(int id, CancellationToken cancellationToken)
    {
        var result = await _getStatement.ExecuteAsync(id, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { errors = result.Errors });
        }

        var document = new StatementOfAccountDocument(result.Data!);
        var bytes = document.GeneratePdf();

        return File(bytes, "application/pdf", $"SOA-{id}-{DateTime.Now:yyyyMMdd}.pdf");
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

        if (result.Succeeded)
        {
            return NoContent();
        }

        // A rejected payload is a validation failure (400); a missing customer is a 404.
        // The two are distinguished by the result type, never by the message text.
        return result is CustomerNotFoundResult
            ? NotFound(new { errors = result.Errors })
            : BadRequest(new { errors = result.Errors });
    }
}
