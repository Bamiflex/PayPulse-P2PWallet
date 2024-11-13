
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
using Microsoft.EntityFrameworkCore;
using P2PWallet.Models;
using static P2PWallet.Models.Models.PaystackVerificationResponse;
using System.Security.Cryptography;
using P2PWallet.Models.Dtos;
using Microsoft.Extensions.Logging;

namespace P2PWallet.Services.Services
{
    public class PaystackService : IPaystackService
    {
        private readonly HttpClient _httpClient;
        private readonly string _paystackSecretKey;
        private readonly P2PWalletDbContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaystackService> _logger;


        public PaystackService(HttpClient httpClient, IConfiguration configuration, P2PWalletDbContext dbContext, ILogger<PaystackService> logger)
        {
            _httpClient = httpClient;
            _paystackSecretKey = configuration["Paystack:SecretKey"];
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _paystackSecretKey);
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            var response = await _httpClient.PostAsync("https://api.paystack.co/transaction/initialize", content);
            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to initialize payment with Paystack. Response: {errorResponse}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonConvert.DeserializeObject<PaystackVerificationResponse>(responseContent);


            if (jsonResponse != null && jsonResponse.status)
            {
                var paymentUrl = jsonResponse.data?.authorization_url;
                return paymentUrl ?? throw new Exception("Payment URL not found in response.");
            }
            else
            {
                throw new Exception($"Error from Paystack: {jsonResponse?.message}");
            }
        }
        public bool IsRequestFromAllowedIp(string remoteIp)
        {
            var allowedIps = _configuration.GetSection("Paystack:AllowedIPs").Get<string[]>();
            return allowedIps.Contains(remoteIp);
        }

        public async Task ProcessTransactionAsync(string reference, string eventType)
        {
            // Ensure the event type is "charge.success" before processing
            if (eventType != "charge.success")
            {
                _logger.LogError("Event type is not 'charge.success'. Ignoring transaction.");
                Console.WriteLine("Event type is not 'charge.success'. Ignoring...");
                return;
            }

            try
            {
                // Retrieve the transaction from the database based on the reference
                var transaction = await _dbContext.Transactions
                    .FirstOrDefaultAsync(t => t.ExternalTransactionId == reference);

                if (transaction == null)
                {
                    Console.WriteLine($"Transaction with reference {reference} not found.");
                    return;
                }

                // Check if the transaction status is already updated to "Success"
                if (transaction.Status == "Success")
                {
                    Console.WriteLine($"Transaction with reference {reference} is already marked as successful.");
                    return;
                }


                transaction.Status = "Success";

                // Assuming transaction.Amount was already set and represents the amount in kobo
                transaction.BalanceAfterTransaction += transaction.Amount;

                // Fetch the associated account
                var account = await _dbContext.Accounts.FindAsync(transaction.AccountId);
                if (account == null)
                {
                    Console.WriteLine($"Account with ID {transaction.AccountId} not found for transaction {reference}.");
                    return;
                }

                // Update the account balance
                account.Balance += transaction.Amount;


                using (var dbContextTransaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        _dbContext.Transactions.Update(transaction);
                        _dbContext.Accounts.Update(account);
                        await _dbContext.SaveChangesAsync();


                        await dbContextTransaction.CommitAsync();
                        Console.WriteLine($"Transaction and account balance updated successfully for reference {reference}.");
                    }
                    catch (Exception ex)
                    {

                        await dbContextTransaction.RollbackAsync();
                        _logger.LogError(ex, $"Error updating transaction or account balance for reference {reference}.");
                        Console.WriteLine($"Error updating transaction or account balance: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in ProcessTransactionAsync.");
                Console.WriteLine($"An unexpected error occurred in ProcessTransactionAsync: {ex.Message}");
            }
        }


        public bool VerifyPaystackSignature(string jsonPayload, string paystackSignature)
        {
            if (string.IsNullOrEmpty(jsonPayload))
            {
                _logger.LogError("jsonPayload is null or empty in VerifyPaystackSignature");
                throw new ArgumentNullException(nameof(jsonPayload), "Payload cannot be null or empty");
            }

            /*

            if (string.IsNullOrEmpty(paystackSignature))
            {
                _logger.LogError("paystackSignature is null or empty in VerifyPaystackSignature");
                throw new ArgumentNullException(nameof(paystackSignature), "Signature cannot be null or empty");
            }
            */

            try
            {
                byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
                string secret = _paystackSecretKey;

                using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret)))
                {
                    byte[] hashBytes = hmac.ComputeHash(payloadBytes);
                    string computedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                    bool isValid = computedSignature == paystackSignature.ToLower();

                    if (!isValid)
                    {
                        _logger.LogWarning("Invalid Paystack signature. Expected: {ComputedSignature}, Received: {PaystackSignature}",
                            computedSignature, paystackSignature);
                    }
                    else
                    {
                        _logger.LogInformation("Paystack signature verified successfully.");
                    }

                    return isValid;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while verifying the Paystack signature");
                return false;
            }
        }



        public async Task<(bool isSuccessful, decimal amount, string verificationStatus)> VerifyPayment(string reference)
        {
            var url = $"https://api.paystack.co/transaction/verify/{reference}";


            var response = await _httpClient.GetAsync(url);


            if (!response.IsSuccessStatusCode)
            {
                var errorResponse = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to verify payment with Paystack. Response: {errorResponse}");
            }


            var responseContent = await response.Content.ReadAsStringAsync();
            var jsonResponse = JsonConvert.DeserializeObject<PaystackVerificationResponse>(responseContent);


            if (jsonResponse != null && jsonResponse.status) 
            {
                decimal amount = jsonResponse.data?.amount ?? 0;
                string verificationStatus = jsonResponse.data?.status ?? "Failed";

                return (true, amount, verificationStatus);
            }
            else
            {
                return (false, 0, jsonResponse?.message ?? "Unknown error during verification.");
            }
        }





        public async Task<Transaction> GetPendingTransactionByUserId(int userId)
        {
            return await _dbContext.Transactions
                .Where(t => t.Account.UserId == userId && t.Status == "Pending")
                .OrderByDescending(t => t.Date) 
                .FirstOrDefaultAsync();
        }


        public async Task<bool> UpdateTransactionStatus(string reference, string status)
        {
            var transaction = await _dbContext.Transactions.FirstOrDefaultAsync(t => t.ExternalTransactionId == reference);

            if (transaction == null)
            {
                throw new InvalidOperationException("Transaction not found.");
            }

            transaction.Status = status;
            await _dbContext.SaveChangesAsync();
            return true;
        }




        
        public async Task<(bool isSuccessful, decimal amount)> VerifyLastPayment(string reference)
        {
            var response = await _httpClient.GetAsync($"https://api.paystack.co/transaction/verify/{reference}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<PaystackVerificationResponse>(json);

                if (result?.status == true && result.data?.status == "success")
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
