using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Models;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace P2PWallet.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto userRegistrationDto)
        {
            if (userRegistrationDto == null)
            {
                return BadRequest(new ApiResponse<string>(false, "User registration data is required.", null));
            }

            try
            {
                var registeredUser = await _userService.RegisterUserAsync(userRegistrationDto);
                return Ok(new ApiResponse<int>(true, "User registered successfully.", registeredUser.Id));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Registration failed for user: {Username}", userRegistrationDto.Username);
                return BadRequest(new ApiResponse<string>(false, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during user registration.");
                return StatusCode(500, new ApiResponse<string>(false, "An error occurred", null));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
        {
            if (loginDto == null || string.IsNullOrEmpty(loginDto.Username) || string.IsNullOrEmpty(loginDto.Password))
            {
                return BadRequest(new ApiResponse<string>(false, "Invalid Login Data", null));
            }

            try
            {
                // Login the user
                var loginResult = await _userService.LoginUserAsync(loginDto.Username, loginDto.Password);

                // Check if the user has set a transaction PIN
                bool needsPin = string.IsNullOrEmpty(loginResult.TransactionPinHash);  // Assuming this is a property in loginResult

                // Prepare response data with the new flag
                var responseData = new
                {
                    token = loginResult.Token,
                    accountNumber = loginResult.AccountNumber,
                    balance = loginResult.Balance,
                    needsPin = loginResult.NeedsPin  // This flag tells the frontend if the user needs to set a PIN
                };

                return Ok(new ApiResponse<object>(true, "Login successful", responseData));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Login failed for user: {Username}", loginDto.Username);
                return Unauthorized(new ApiResponse<string>(false, ex.Message, null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred during login.");
                return StatusCode(500, new ApiResponse<string>(false, "An error occurred during login", null));
            }
        }


    }
}
