
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using P2PWallet.Models.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;

namespace P2PWallet.Services.Services
{
    public class PaystackService : IPaystackService
    {
        private readonly HttpClient _httpClient;
        private readonly string _paystackSecretKey;

        public PaystackService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _paystackSecretKey = configuration["Paystack:SecretKey"];
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);
        }



            public async Task<string> InitializePayment(decimal amount, string email, string reference)
            {

            
            var requestBody = new
                {
                    amount = (int)(amount * 100),
                    email,
                    reference,
                    currency = "NGN"
                };

                var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

            // Set up the request headers
           // _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _paystackSecretKey);


            var response = await _httpClient.PostAsync("https://api.paystack.co/transaction/initialize", content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorResponse = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to initialize payment with Paystack. Response: {errorResponse}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var jsonResponse = JsonConvert.DeserializeObject<PaystackVerificationResponse>(responseContent);



                if (jsonResponse != null && jsonResponse.status == "true")
            {
                var paymentUrl = jsonResponse.data?.authorization_url;
                return paymentUrl ?? throw new Exception("Payment URL not found in response.");
            }
            else
            {
                throw new Exception($"Error from Paystack: {jsonResponse?.message}");
            }
        }

        public async Task<(bool isSuccessful, decimal amount)> VerifyPayment(string reference)
        {
            var response = await _httpClient.GetAsync($"https://api.paystack.co/transaction/verify/{reference}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PaystackVerificationResponse>(json);

                if (result?.status == "true" && result.data?.status == "success")
                {
                    return (true, result.data.amount / 100);
                }
                else
                {
                    throw new Exception($"Payment verification failed: {result?.message}");
                }
            }

            return (false, 0);
        }

    }
}
