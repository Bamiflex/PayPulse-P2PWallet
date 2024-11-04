/*using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace P2PWallet.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;
        private readonly P2PWalletDbContext _dbContext;
        public UserController(IUserService userService, ILogger<UserController> logger, P2PWalletDbContext dbContext
            )
        {
            _userService = userService;
            _logger = logger;
            _dbContext = dbContext;
        }
        private int GetUserIdFromToken()
        {
            // Get the token from the Authorization header
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            if (string.IsNullOrEmpty(token))
            {
                throw new InvalidOperationException("No token found in the request.");
            }

            // Validate the token and extract claims
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken == null || !jwtToken.Claims.Any())
            {
                throw new InvalidOperationException("Invalid token format.");
            }

            // Retrieve the UserId claim
            var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "UserId");
            if (userIdClaim == null)
            {
                throw new InvalidOperationException("User ID not found in token.");
            }

            // Parse and return UserId as integer
            if (int.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            throw new InvalidOperationException("Invalid User ID format in token.");
        }


        [HttpPost("signup")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto userRegistrationDto)
        {
            if (userRegistrationDto == null)
            {
                return BadRequest(new { Message = "User registration data is required." });
            }

            try
            {
                // Call the service to register the user
                var registeredUser = await _userService.RegisterUserAsync(userRegistrationDto);

                // Return success response with user ID
                return Ok(new { Message = "User registered successfully", UserId = registeredUser.Id });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Registration failed for user: {Username}", userRegistrationDto.Username);
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during user registration.");
                return BadRequest(new { Message = ex.Message });
            }
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (loginDto == null || string.IsNullOrEmpty(loginDto.Username) || string.IsNullOrEmpty(loginDto.Password))
            {
                return BadRequest("Invalid login data.");
            }
            try
            {   // Attempt login and retrieve user details along with token
                var loginResult = await _userService.LoginUserAsync(loginDto.Username, loginDto.Password);

                return Ok(new
                {
                    token = loginResult.Token,
                    accountNumber = loginResult.AccountNumber,
                    balance = loginResult.Balance
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Login failed for user: {Username}", loginDto.Username);
                return Unauthorized(new { Message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("change-pin")]
        public async Task<IActionResult> ChangeTransactionPin([FromBody] TransactionPinDto transactionPinDto)
        {
            if (string.IsNullOrEmpty(transactionPinDto.TransactionPin))
            {
                return BadRequest("Transaction PIN is required.");
            }

            var userId = GetUserIdFromToken(); // Implement this method to get the user ID from the JWT token
            if (userId == null)
            {
                return Unauthorized("User ID not found in token.");
            }



            await _userService.SetTransactionPinAsync(userId, transactionPinDto.TransactionPin);
            return Ok("Transaction PIN set successfully.");
        }

        [Authorize]
        [HttpGet("{accountNumber}/transactions")]
        public async Task<IActionResult> GetTransactions(string accountNumber)
        {
            var transactions = await _dbContext.Transactions
                .Where(t => t.AccountNumber == accountNumber)
                .OrderByDescending(t => t.Date)
                .ToListAsync();

            return Ok(transactions);
        }


        [Authorize]
        [HttpGet("{accountNumber}/balance")]
        public async Task<IActionResult> GetBalance(string accountNumber)
        {
            try
            {
                var balance = await _userService.GetBalanceAsync(accountNumber);
                return Ok(new { AccountNumber = accountNumber, Balance = balance });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching balance for account: {AccountNumber}", accountNumber);
                return StatusCode(500, "Internal server error");
            }
        }

        [Authorize]
        [HttpPost("transfer")]
        public async Task<IActionResult> TransferFunds([FromBody] TransferFundsDto transferFundsDto)
        {
            if (transferFundsDto == null)
            {
                return BadRequest(new { Message = "Transfer details are required." });
            }

            try
            {
                var success = await _userService.TransferFundsAsync(
                    transferFundsDto.FromAccount, 
                    transferFundsDto.ToAccount, 
                    transferFundsDto.Amount, 
                    transferFundsDto.TransactionPin);
                if (success)
                    return Ok(new { Message = "Transfer successful" });
                return BadRequest(new { Message = "Transfer failed" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "An unexpected error occurred during fund transfer.");
                throw;
            }
        }


       
    }
}
*/

using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace P2PWallet.Controllers
{
    [Route("api/user")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<UserController> _logger;

        public UserController(IUserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        private int GetUserIdFromToken()
        {
            var token = HttpContext.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadToken(token) as JwtSecurityToken;
            var userIdClaim = jwtToken?.Claims.FirstOrDefault(claim => claim.Type == "UserId");
            if (int.TryParse(userIdClaim?.Value, out var userId))
            {
                return userId;
            }
            throw new InvalidOperationException("Invalid User ID format in token.");
        }
        /*
        [Authorize]
        [HttpPost("change-pin")]
        public async Task<IActionResult> ChangeTransactionPin([FromBody] TransactionPinDto transactionPinDto)
        {
            if (string.IsNullOrEmpty(transactionPinDto.TransactionPin))
            {
                return BadRequest("Transaction PIN is required.");
            }

            var userId = GetUserIdFromToken();
            await _userService.SetTransactionPinAsync(userId, transactionPinDto.TransactionPin);
            return Ok("Transaction PIN set successfully.");
        }*/

        [Authorize]
        [HttpPost("change-pin")]
        public async Task<IActionResult> ChangeTransactionPin([FromBody] TransactionPinDto transactionPinDto)
        {
            if (string.IsNullOrEmpty(transactionPinDto.TransactionPin))
            {
                return BadRequest(new ApiResponse<string>(false, "Transaction PIN is required", null));
            }

            try
            {
                var userId = GetUserIdFromToken();
                await _userService.SetTransactionPinAsync(userId, transactionPinDto.TransactionPin);
                return Ok(new ApiResponse<string>(true, "Transaction PIN set successfully", null));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to set Transaction PIN for user");
                return BadRequest(new ApiResponse<string>(false, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while setting Transaction PIN");
                return StatusCode(500, new ApiResponse<string>(false, "An error occurred while setting Transaction PIN", null));
            }
        }


    }
}
