using DigitalBanking.Domain.Entities;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DigitalBanking.Application.DTOs.Customers;
using AutoMapper;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly BankDbContext _db;
    private readonly IMapper _mapper;

    public CustomersController(BankDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    // GET: api/customers
    [HttpGet]
    public async Task<ActionResult<List<CustomerResponse>>> GetAll()
    {
        var customers = await _db.Customers
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        var response = _mapper.Map<List<CustomerResponse>>(customers);

        return Ok(response);
    }

    // GET: api/customers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id)
    {
        var customer = await _db.Customers.FindAsync(id);

        if (customer is null)
            return NotFound();

        var response = _mapper.Map<CustomerResponse>(customer);

        return Ok(response);
    }

    // POST: api/customers
    [HttpPost]
    public async Task<ActionResult<CustomerResponse>> Create(
        [FromBody] CreateCustomerRequest request)
    {
        var customer = _mapper.Map<Customer>(request);
        customer.Id = Guid.NewGuid();
        customer.CreatedDate = DateTime.UtcNow;

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        var response = _mapper.Map<CustomerResponse>(customer);

        return CreatedAtAction(nameof(GetById),
            new { id = customer.Id },
            response);
    }

    // PUT: api/customers/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Customer request)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        customer.FirstName = request.FirstName;
        customer.LastName = request.LastName;
        customer.IdentityNumber = request.IdentityNumber;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE: api/customers/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();

        _db.Customers.Remove(customer);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
