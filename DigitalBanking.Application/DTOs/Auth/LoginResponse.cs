using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Application.DTOs.Auth
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid? CustomerId { get; set; }
        public string? DisplayName { get; set; }
    }
}
