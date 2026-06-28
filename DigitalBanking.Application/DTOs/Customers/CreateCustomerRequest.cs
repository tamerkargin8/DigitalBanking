using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Customers;

public class CreateCustomerRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
}
