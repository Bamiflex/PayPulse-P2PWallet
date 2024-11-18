﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using P2PWallet.Models;
using P2PWallet.Models.Dtos;

namespace P2PWallet.Services.Services
    {
        public interface IPaystackService
        {
            /// <summary>
            /// Initializes a payment with Paystack and returns the payment URL.
            /// </summary>
            /// <param name="amount">Amount to be charged in kobo (e.g., for NGN 100.00, use 10000).</param>
            /// <param name="email">Customer's email address.</param>
            /// <param name="reference">Unique reference for the transaction.</param>
            /// <returns>The payment URL if successful.</returns>
            Task<string> InitializePayment(decimal amount, string email, string reference);
            // Task<string> InitializePayment(decimal amount, string email, string reference);
            /// <summary>
            /// Verifies the status of a payment with Paystack.
            /// </summary>
            /// <param name="reference">Unique reference of the transaction to verify.</param>
            /// <returns>True if payment was successful, otherwise false.</returns>
            Task<(bool isSuccessful, decimal amount)> VerifyLastPayment(string reference);
            Task<Transaction> GetPendingTransactionByUserId(int userId);
            Task<(bool isSuccessful, decimal amount, string verificationStatus)> VerifyPayment(string reference);

            Task<bool> ProcessTransactionAsync(string reference, string eventType, decimal amountReceived);
            bool VerifyPaystackSignature(string jsonPayload, string paystackSignature);
            bool IsRequestFromAllowedIp(string remoteIp);
            int GetAccountIdByEmail(string email);


    }
    }

