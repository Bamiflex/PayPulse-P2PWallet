using P2PWallet.Models;


namespace P2PWallet.Models
{
    public class LoginDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class InitializeDto
    {
        public decimal Amount { get; set; }
        public string Email { get; set; }
    }
}
