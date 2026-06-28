using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Accounts;

public class AccountResponse
{
    public Guid Id { get; set; }

    public string AccountNumber { get; set; } = string.Empty;

    public decimal Balance { get; set; }

    public string Currency { get; set; } = "TRY";

    public Guid CustomerId { get; set; }

    public DateTime CreatedDate { get; set; }
}
