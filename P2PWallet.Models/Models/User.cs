
using P2PWallet.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace P2PWallet.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
        public string PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }
        public string TransactionPinHash { get; set; }
        public byte[] PinSalt { get; set; }
        
        private bool isDefaultPin;

        [Column("IsDefaultPin")]
        public bool IsDefaultPin
        {
            get => isDefaultPin == false; // Treat `false` (0) as true
            set => isDefaultPin = !value; // Invert the value before storing it
        }


        // Navigation property to link User with Account
        public Account Account { get; set; }
    }
}
