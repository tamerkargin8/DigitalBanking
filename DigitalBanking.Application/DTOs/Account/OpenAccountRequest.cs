using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Account;

public class OpenAccountRequest
{
    public Guid CustomerId { get; set; }
}
