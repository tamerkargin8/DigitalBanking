using DigitalBanking.Domain.Entities;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly BankDbContext _db;

    public CustomersController(BankDbContext db)
    {
        _db = db;
    }

    // GET: api/customers
    [HttpGet]
    public async Task<ActionResult<List<Customer>>> GetAll()
    {
        var customers = await _db.Customers
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync();

        return Ok(customers);
    }

    // GET: api/customers/{id}
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Customer>> GetById(Guid id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null) return NotFound();
        return Ok(new
        {
            customer.Id,
            customer.FirstName,
            customer.LastName,
            customer.IdentityNumber,
            customer.CreatedDate
        });
    }

    // POST: api/customers
    [HttpPost]
    public async Task<ActionResult<Customer>> Create(Customer request)
    {
        request.Id = Guid.NewGuid();
        request.CreatedDate = DateTime.UtcNow;

        _db.Customers.Add(request);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
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
