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
            [StringLength(20)] 
            public string AccountNumber { get; set; }

            [Required]
            public DateTime Date { get; set; } = DateTime.Now;

            [Required]
            [Column(TypeName = "decimal(18,2)")]
            public decimal Amount { get; set; }

            [Required]
            public TransactionType Type { get; set; }

            [Required]
            [Column(TypeName = "decimal(18,2)")]
            public decimal BalanceAfterTransaction { get; set; }

            public string Description { get; set; }

            [Required]
            public int AccountId { get; set; }


            [Required]
            [StringLength(10)] // To store values like "Pending", "Success", "Failed"
            public string Status { get; set; } = "Pending";

            public string ExternalTransactionId { get; set; }

            public virtual Account Account { get; set; }
        }

        public enum TransactionType
        {
            Credit,
            Debit
        }

    public class GeneralLedger
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } // "Credit" or "Debit"
        public decimal RunningBalance { get; set; }
        public string reference { get; set; }
    }

}
