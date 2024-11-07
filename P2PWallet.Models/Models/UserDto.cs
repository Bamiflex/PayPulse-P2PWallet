
using P2PWallet.Models;



namespace P2PWallet.Models
{
    public class UserDto
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
    }

    public class TransactionPinDto
    {
        public string TransactionPin { get; set; }
        public string ConfirmTransactionPin { get; set; }
    }
}
