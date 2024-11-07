
using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Services.Services;
using System;
using System.Threading.Tasks;
using P2PWallet.Models;
using Microsoft.EntityFrameworkCore;
using static P2PWallet.Models.Models.PaystackVerificationResponse;

namespace P2PWallet.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaystackController : ControllerBase
    {
        private readonly IPaystackService _paystackService;
        private readonly IUserService _userService;

        public PaystackController(IPaystackService paystackService, IUserService userService)
        {
            _paystackService = paystackService;
            _userService = userService;
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

        [HttpPost("initialize")]
        public async Task<IActionResult> InitializePayment([FromBody] InitializePaymentRequest initializePaymentRequest)
        {
            // Validate the incoming request
            if (initializePaymentRequest == null || string.IsNullOrEmpty(initializePaymentRequest.Email) || initializePaymentRequest.Amount <= 0)
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
                var paymentUrl = await _paystackService.InitializePayment(initializePaymentRequest.Amount, initializePaymentRequest.Email, reference);

                var accountNumber = GetAccountNumberFromToken();
                var userId = GetUserIdFromToken();

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


        [HttpGet("verify")]
        public async Task<IActionResult> VerifyPayment([FromQuery] VerifyPaymentDto verifyPaymentDto)
        {
            try
            {
                if (string.IsNullOrEmpty(verifyPaymentDto.Reference))
                {
                    return BadRequest(new
                    {
                        status = false,
                        statusMessage = "Transaction reference is required."
                    });
                }

                var userId = GetUserIdFromToken();
                var (isSuccessful, amount) = await _paystackService.VerifyPayment(verifyPaymentDto.Reference);

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
                    statusMessage = $"Failed to verify payment: {ex.Message}"
                });
            }
        }




    }
}
