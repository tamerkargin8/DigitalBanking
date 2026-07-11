using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalBanking.Domain.Entities
{
    public class AuditLog
    {
        public Guid Id { get; set; }

        public string? Username { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public string? EntityId { get; set; } = string.Empty;

        public bool IsSuccess { get; set; }

        public string? Details { get; set; }

        public DateTime CreatedDate { get; set; }
    }
}
