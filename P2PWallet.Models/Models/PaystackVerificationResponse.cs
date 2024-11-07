
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PWallet.Models.Models
{
    public class PaystackVerificationResponse
    {
        public string status { get; set; }
        public string message { get; set; }
        public Data data { get; set; }

        public class Data
        {
            public string status { get; set; }
            public decimal amount { get; set; }
            public string currency { get; set; }
            public string reference { get; set; }
            public string authorization_url { get; set; }

        }

        public class InitializePaymentRequest
        {
            [Required]
            public string Email { get; set; }

            [Required]
            [Range(1, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
            public decimal Amount { get; set; }

        }

        public class VerifyPaymentDto
        {
            public string Reference { get; set; }

        }

        public class VerifyPaymentResponseDto
        {
            public bool Status { get; set; }
            public string StatusMessage { get; set; }
            public string Reference { get; set; }
            public decimal Amount { get; set; }
            public string PaymentStatus { get; set; }
        }

    }

}

