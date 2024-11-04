using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using P2PWallet.Models;

namespace P2PWallet.Models
{
    public class Transaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string AccountNumber { get; set; } // Associate with user's account

        [Required]
        public DateTime Date { get; set; } = DateTime.Now; // Date and time of the transaction with a default value

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public string Type { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfterTransaction { get; set; }

        public string Description { get; set; }

        [Required]
        public int AccountId { get; set; }

        // Navigation property to Account
        public virtual Account Account { get; set; }
    }
}
