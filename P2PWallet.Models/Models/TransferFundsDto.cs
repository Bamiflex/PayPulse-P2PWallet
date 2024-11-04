using P2PWallet.Models;


namespace P2PWallet.Models
{
    public class TransferFundsDto
    {
        public string ToAccount { get; set; }
        public decimal Amount { get; set; }
        public string TransactionPin {  get; set; }
    }
}
