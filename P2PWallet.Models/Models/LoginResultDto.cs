using P2PWallet.Models;

namespace P2PWallet.Models
{
    public class LoginResultDto
    {
        public string Token { get; set; }
        public string AccountNumber { get; set; }
        public decimal Balance { get; set; }
    }
}
