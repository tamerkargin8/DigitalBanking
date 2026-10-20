using AutoMapper;
using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Customers;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner")]
public class CustomersController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;

    public CustomersController(BankDbContext db, IMapper mapper, IAuditService auditService)
    {
        _db = db;
        _mapper = mapper;
        _auditService = auditService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<CustomerResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default)
    {
        var customers = await _db.Customers
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<CustomerResponse>>.SuccessWith(
            _mapper.Map<List<CustomerResponse>>(customers)));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (customer is null)
            throw new NotFoundException("Customer", id);

        return Ok(ApiResponse<CustomerResponse>.SuccessWith(_mapper.Map<CustomerResponse>(customer)));
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<CustomerResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var customer = _mapper.Map<Customer>(request);
        customer.Id = Guid.NewGuid();
        customer.CreatedDate = DateTime.UtcNow;
        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.IdentityNumber = request.IdentityNumber.Trim();

        _db.Customers.Add(customer);
        _auditService.Add(
            action: "CUSTOMER_CREATED",
            entityName: "Customer",
            entityId: customer.Id.ToString(),
            username: User.GetActor().Username,
            details: "Synthetic demo customer created.");

        await _db.SaveChangesAsync(cancellationToken);

        var response = _mapper.Map<CustomerResponse>(customer);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id },
            ApiResponse<CustomerResponse>.SuccessWith(response, "Customer created successfully."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
            throw new NotFoundException("Customer", id);

        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.IdentityNumber = request.IdentityNumber.Trim();

        _auditService.Add(
            action: "CUSTOMER_UPDATED",
            entityName: "Customer",
            entityId: customer.Id.ToString(),
            username: User.GetActor().Username,
            details: "Synthetic demo customer updated.");

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Customer updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
            throw new NotFoundException("Customer", id);

        _db.Customers.Remove(customer);
        _auditService.Add(
            action: "CUSTOMER_DELETED",
            entityName: "Customer",
            entityId: customer.Id.ToString(),
            username: User.GetActor().Username,
            details: "Synthetic demo customer deleted.");

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse.SuccessResponse("Customer deleted successfully."));
    }

    private static void Validate(CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || request.FirstName.Trim().Length > 100)
            throw new BusinessRuleException("First name is required and cannot exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(request.LastName) || request.LastName.Trim().Length > 100)
            throw new BusinessRuleException("Last name is required and cannot exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(request.IdentityNumber) || request.IdentityNumber.Trim().Length > 50)
            throw new BusinessRuleException("Identity number is required and cannot exceed 50 characters.");
    }
}
