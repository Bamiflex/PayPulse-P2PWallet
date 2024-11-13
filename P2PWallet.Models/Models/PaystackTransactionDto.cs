using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PWallet.Models.Dtos
{
    public class PaystackTransactionDto
    {
        public string Event { get; set; }
        public PaystackDataDto Data { get; set; }
    }

    public class PaystackDataDto
    {
        public string Status { get; set; }
        public string Reference { get; set; }
        public decimal Amount { get; set; }
        public string GatewayResponse { get; set; }
        public DateTime PaidAt { get; set; }
        public string Currency { get; set; }
        public PaystackCustomerDto Customer { get; set; }
        public PaystackAuthorizationDto Authorization { get; set; }
    }

    public class PaystackCustomerDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
    }

    public class PaystackAuthorizationDto
    {
        public string AuthorizationCode { get; set; }
        public string Bank { get; set; }
        public string CardType { get; set; }
    }
}
