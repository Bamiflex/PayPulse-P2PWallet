
using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Services.Services;
using System;
using System.Threading.Tasks;
using P2PWallet.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using static P2PWallet.Models.Models.PaystackVerificationResponse;
using P2PWallet.Models.Dtos;
using Newtonsoft.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;


namespace P2PWallet.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaystackController : ControllerBase
    {
        private readonly IPaystackService _paystackService;
        private readonly IUserService _userService;
        private readonly ILogger<PaystackController> _logger;

        public PaystackController(IPaystackService paystackService, IUserService userService, ILogger<PaystackController> logger)
        {
            _paystackService = paystackService;
            _userService = userService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private int GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private string GetAccountNumberFromToken()
        {
            var accountNumberClaim = User.FindFirst("AccountNumber")?.Value;
            return accountNumberClaim;
        }

        private string GetEmailFromToken()
        {
            var emailClaim = HttpContext.User.FindFirst(ClaimTypes.Email)
                             ?? HttpContext.User.FindFirst(JwtRegisteredClaimNames.Email);

            return emailClaim?.Value;
        }

        [Authorize]
        [HttpPost("initialize")]
        public async Task<IActionResult> InitializePayment([FromBody] InitializePaymentRequest initializePaymentRequest)
        {
            if (initializePaymentRequest == null || initializePaymentRequest.Amount <= 0)
            {
                return BadRequest(new
                {
                    status = false,
                    statusMessage = "Invalid input parameters.",
                    data = new { }
                });
            }

            try
            {

                var reference = Guid.NewGuid().ToString();


                var email = GetEmailFromToken();


                var paymentUrl = await _paystackService.InitializePayment(initializePaymentRequest.Amount, email, reference);


                var accountNumber = GetAccountNumberFromToken();
                var userId = GetUserIdFromToken();

                // Create a transaction record
                var transaction = new Transaction
                {
                    AccountNumber = accountNumber,
                    Date = DateTime.UtcNow,
                    Amount = initializePaymentRequest.Amount,
                    Type = TransactionType.Credit,
                    BalanceAfterTransaction = 0,
                    Description = "Paystack AddMoney",
                    Status = "Pending",
                    ExternalTransactionId = reference,
                    AccountId = userId
                };

                await _userService.CreateTransactionAsync(transaction);

                return Ok(new
                {
                    status = true,
                    statusMessage = "Payment initialization successful.",
                    data = new { paymentUrl, reference }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    statusMessage = $"Failed to initialize payment: {ex.Message}",
                    data = new { }
                });
            }
        }


        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            string jsonPayload;

            // Read the raw JSON payload from the request body
            using (var reader = new StreamReader(Request.Body))
            {
                jsonPayload = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrEmpty(jsonPayload))
            {
                _logger.LogError("Received empty payload from Paystack");
                return BadRequest("Invalid payload received");
                Console.WriteLine(jsonPayload);
            }

            // Log the received payload
            _logger.LogInformation("Received payload: {Payload}", jsonPayload);
            
            // Retrieve the Paystack signature from headers
            if (!Request.Headers.TryGetValue("X-Paystack-Signature", out var paystackSignature) || string.IsNullOrEmpty(paystackSignature))
            {
                _logger.LogError("Paystack signature missing from headers");
                return BadRequest("Signature is required");
            }

            
            // Verify the Paystack signature
            bool isValid = _paystackService.VerifyPaystackSignature(jsonPayload, paystackSignature);
            if (!isValid)
            {
                _logger.LogWarning("Invalid Paystack signature");
                return Unauthorized("Invalid signature");
            }



            string reference = null;
            string eventType = null;

            using (JsonDocument doc = JsonDocument.Parse(jsonPayload))
            {
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var dataElement) && dataElement.TryGetProperty("reference", out var referenceElement))
                {
                    reference = referenceElement.GetString();
                }

                if (root.TryGetProperty("event", out var eventElement))
                {
                    eventType = eventElement.GetString();
                }
            }

            if (reference == null || eventType == null)
            {
                _logger.LogError("Required fields 'reference' or 'event' are missing in the payload");
                return BadRequest("Invalid payload format");
            }

            _logger.LogInformation("Event: {EventType}, Reference: {Reference}", eventType, reference);

            // Process the transaction based on reference and event type
            await _paystackService.ProcessTransactionAsync(reference, eventType);

            _logger.LogInformation("Webhook processed successfully");
            return Ok();
        }



        [HttpGet("verify")]
        public async Task<IActionResult> VerifyPayment()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var transaction = await _paystackService.GetPendingTransactionByUserId(userId);

                if (transaction == null || string.IsNullOrEmpty(transaction.ExternalTransactionId))
                {
                    return BadRequest(new
                    {
                        status = false,
                        statusMessage = "No pending transaction found for this user.",
                        data = new {}
                    });
                }


                if (transaction.Status == "Success")
                {
                    Console.WriteLine($"Transaction with reference {transaction.ExternalTransactionId} is already marked as successful.");
                    return BadRequest(new
                    {
                        status = false,
                        statusMessage = "Transaction for this user has been marked successful.",
                        data = new { }
                    });
                }


                var (isSuccessful, amount, verificationStatus) = await _paystackService.VerifyPayment(transaction.ExternalTransactionId);

                


                var amountInNaira = amount / 100m;

                if (isSuccessful && verificationStatus == "success")
                {
                    await _userService.AddBalance(userId, amountInNaira);
                    await _userService.UpdateTransactionStatus(transaction.ExternalTransactionId, "Successful");

                    

                    return Ok(new VerifyPaymentResponseDto
                    {
                        Status = true,
                        StatusMessage = "Payment verified and balance updated successfully.",
                        Reference = transaction.ExternalTransactionId,
                        Amount = amountInNaira,
                        PaymentStatus = "Successful"
                    });
                }
                else
                {
                    return Ok(new VerifyPaymentResponseDto
                    {
                        Status = false,
                        StatusMessage = "Payment verification failed or pending.",
                        Reference = transaction.ExternalTransactionId,
                        PaymentStatus = verificationStatus ?? "Failed"
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    statusMessage = $"Failed to verify payment: {ex.Message}",
                    data = new { }
                });
            }
        }

        [HttpGet("verify-last")]
        public async Task<IActionResult> VerifyPayment([FromQuery] VerifyPaymentDto verifyPaymentDto)
        {
            try
            {
                if (string.IsNullOrEmpty(verifyPaymentDto.Reference))
                {
                    return BadRequest(new
                    {
                        status = false,
                        statusMessage = "Transaction reference is required.",
                        data = new { }
                    });
                }

                var userId = GetUserIdFromToken();
                var (isSuccessful, amount) = await _paystackService.VerifyLastPayment(verifyPaymentDto.Reference);

                if (isSuccessful)
                {
                    await _userService.AddBalance(userId, amount);
                    await _userService.UpdateTransactionStatus(verifyPaymentDto.Reference, "Successful");

                    var response = new VerifyPaymentResponseDto
                    {
                        Status = true,
                        StatusMessage = "Payment verified and balance updated successfully.",
                        Reference = verifyPaymentDto.Reference,
                        Amount = amount,
                        PaymentStatus = "Successful"
                    };
                    return Ok(response);
                }
                else
                {
                    var response = new VerifyPaymentResponseDto
                    {
                        Status = false,
                        StatusMessage = "Payment verification failed or pending.",
                        Reference = verifyPaymentDto.Reference,
                        PaymentStatus = "Failed"
                    };
                    return Ok(response);
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    status = false,
                    statusMessage = $"Failed to verify payment: {ex.Message}",
                    data = new { }
                });
            }
        }




    }

}

