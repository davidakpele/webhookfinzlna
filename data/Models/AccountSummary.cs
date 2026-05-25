using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebhooksAPI.data.Models
{
    public class AccountSummary
    {
        public string AccountId { get; set; } = default!;
        public decimal TotalCredits { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal RunningBalance => TotalCredits - TotalDebits;
        public int TransactionCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}