using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebhooksAPI.data.DTO
{
   
    public class InboundTransactionDto
    {
        [Required]
        public string ExternalRef { get; set; } = default!;
    
        [Required]
        public string AccountId { get; set; } = default!;
    
        [Required]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be positive.")]
        public decimal Amount { get; set; }
    
        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = default!;
    
        [Required]
        [RegularExpression("credit|debit", ErrorMessage = "Type must be 'credit' or 'debit'.")]
        public string Type { get; set; } = default!;
    
        [Required]
        [RegularExpression("pending|completed|failed")]
        public string Status { get; set; } = default!;
    
        [Required]
        public DateTime TransactedAt { get; set; }

        public object? Metadata { get; set; }
    }
}