using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2PWallet.Models.Models
{
    public class PaystackVerificationResponse
    {
        public string status { get; set; } // Typically this might be a boolean
        public string message { get; set; }
        public Data data { get; set; } // Add this property to hold the data object

        public class Data
        {
            public string status { get; set; } // "success" or other statuses
            public decimal amount { get; set; } // Amount in kobo
            public string currency { get; set; }
            public string reference { get; set; }
            public string authorization_url { get; set; }
            // Any other relevant fields as necessary
        }

        public class InitializePaymentRequest
        {
            public decimal Amount { get; set; }
            public string Email { get; set; }
        }

    }

}
