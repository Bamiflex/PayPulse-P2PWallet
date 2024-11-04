using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace P2PWallet.Controllers
{
    [Route("api/accounts")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AccountController> _logger;
        private readonly P2PWalletDbContext _dbContext;

        public AccountController(IUserService userService, ILogger<AccountController> logger, P2PWalletDbContext dbContext)
        {
            _userService = userService;
            _logger = logger;
            _dbContext = dbContext;
        }


       

    private string GetAccountNumberFromToken()
        {
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
            var accountNumberClaim = jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "AccountNumber");

            if (accountNumberClaim != null)
            {
                return accountNumberClaim.Value;
            }

            throw new InvalidOperationException("Account number not found in token.");
        }

        [Authorize]
        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            try
            {
                var accountNumber = GetAccountNumberFromToken();

                var balance = await _userService.GetBalanceAsync(accountNumber);
                return Ok(new ApiResponse<object>(true, "Balance retrieved successfully", new { AccountNumber = accountNumber, Balance = balance }));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve balance for account: {AccountNumber}", ex.Message);
                return NotFound(new ApiResponse<string>(false, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching balance for account.");
                return StatusCode(500, new ApiResponse<string>(false, "Internal server error", null));
            }
        }

        [Authorize]
        [HttpGet("transactions-history")]
        public async Task<IActionResult> GetTransactions()
        {
            try
            {
                var accountNumber = GetAccountNumberFromToken();

                var transactions = await _dbContext.Transactions
                    .Where(t => t.AccountNumber == accountNumber)
                    .OrderByDescending(t => t.Date)
                    .ToListAsync();

                return Ok(new ApiResponse<object>(true, "Transactions retrieved successfully", transactions));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while retrieving transactions.");
                return StatusCode(500, new ApiResponse<string>(false, "Internal server error", null));
            }
        }

        [Authorize]
        [HttpPost("transfer")]
        public async Task<IActionResult> TransferFunds([FromBody] TransferFundsDto transferFundsDto)
        {
            if (transferFundsDto == null)
            {
                return BadRequest(new ApiResponse<string>(false, "Transfer details are required", null));
            }

            if (transferFundsDto.Amount <= 0)
            {
                return BadRequest(new ApiResponse<string>(false, "Invalid transaction amount", null));
            }

            try
            {
                var fromAccountNumber = GetAccountNumberFromToken();

                var success = await _userService.TransferFundsAsync(
                    fromAccountNumber, // Use the account number from the token
                    transferFundsDto.ToAccount,
                    transferFundsDto.Amount,
                    transferFundsDto.TransactionPin);


                if (fromAccountNumber == transferFundsDto.ToAccount)
                {
                    return BadRequest(new
                    {
                        status = false,
                        statusMessage = "Transfer to the same account is not allowed.",
                        data = ""
                    });
                }

                return success
                    ? Ok(new ApiResponse<string>(true, "Transfer successful", null))
                    : BadRequest(new ApiResponse<string>(false, "Transfer failed", null));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Transfer failed due to invalid operation.");
                return BadRequest(new ApiResponse<string>(false, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during fund transfer.");
                return StatusCode(500, new ApiResponse<string>(false, "Internal server error", null));
            }
        }

        [Authorize]
        [HttpGet("get-account-name")]
        public async Task<IActionResult> GetAccountName(string accountNumber)
        {
            if (string.IsNullOrEmpty(accountNumber))
            {
                return BadRequest(new { status = false, statusMessage = "Account number is required." });
            }

            var accountName = await _userService.GetAccountNameByAccountNumberAsync(accountNumber);

            if (string.IsNullOrEmpty(accountName))
            {
                return NotFound(new { status = false, statusMessage = "Account not found." });
            }

            return Ok(new { status = true, statusMessage = "Account found.", data = accountName });
        }



    }
}
