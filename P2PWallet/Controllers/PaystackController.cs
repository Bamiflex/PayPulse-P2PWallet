using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Services.Services;
using System;
using System.Threading.Tasks;
using P2PWallet.Models;

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

        /// <summary>
        /// Initializes a payment and returns a Paystack authorization URL.
        /// </summary>
        /// <param name="amount">Amount to be charged in Naira.</param>
        /// <param name="email">Customer's email address.</param>
        /// <returns>Authorization URL from Paystack.</returns>
        [HttpPost("initialize")]
        public async Task<IActionResult> InitializePayment([FromBody] InitializeDto initializeDto)
        {
            if (initializeDto == null || string.IsNullOrEmpty(initializeDto.Email) || initializeDto.Amount <= 0)
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
                var reference = Guid.NewGuid().ToString(); // Generate a unique reference for the transaction
                var paymentUrl = await _paystackService.InitializePayment(initializeDto.Amount, initializeDto.Email, reference);

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


        /// <summary>
        /// Verifies the payment status with the given reference.
        /// </summary>
        /// <param name="reference">Unique transaction reference.</param>
        /// <returns>Status of the payment verification.</returns>

        [HttpGet("verify")]
        public async Task<IActionResult> VerifyPayment(string reference, int userId)
        {
            try
            {
                var (isSuccessful, amount) = await _paystackService.VerifyPayment(reference);

                if (isSuccessful)
                {
                    // Update the user's account balance
                    await _userService.AddBalance(userId, amount);

                    return Ok(new
                    {
                        status = true,
                        statusMessage = "Payment verified and balance updated successfully.",
                        data = new { reference, amount, status = "success" }
                    });
                }
                else
                {
                    return Ok(new
                    {
                        status = false,
                        statusMessage = "Payment verification failed or pending.",
                        data = new { reference, status = "failed" }
                    });
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