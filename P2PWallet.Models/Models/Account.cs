using P2PWallet.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace P2PWallet.Models
{
    public class Account
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AccountId { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 10, ErrorMessage = "Account number must be 10 digits")]
        public string AccountNumber { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        [Required]
        [StringLength(3)]
        public string Currency { get; set; } = "NGN";

        [Required]
        public int UserId { get; set; }

        public User User { get; set; }
    }
}
