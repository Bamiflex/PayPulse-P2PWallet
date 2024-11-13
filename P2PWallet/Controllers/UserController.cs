using Microsoft.AspNetCore.Mvc;
using P2PWallet.Services;
using P2PWallet.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using P2PWallet.Models.Models;

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

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto)
        {
            if (changePasswordDto.NewPassword != changePasswordDto.ConfirmPassword)
            {
                return BadRequest("New Password and Confirm Password do not match.");
            }

            var userId = int.Parse(User.FindFirst("UserId").Value); // Get user ID from token
            var success = await _userService.ChangePasswordAsync(userId, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);

            if (!success)
            {
                return BadRequest("Failed to change password.");
            }

            return Ok(new { status = true, statusMessage = "Password changed successfully." });
        }

        [Authorize]
        [HttpPost("set-pin")]
        public async Task<IActionResult> ChangeTransactionPin([FromBody] TransactionPinDto transactionPinDto)
        {

            if (string.IsNullOrEmpty(transactionPinDto.TransactionPin) || string.IsNullOrEmpty(transactionPinDto.ConfirmTransactionPin))
            {
                return BadRequest(new ApiResponse<string>(false, "Both Transaction PIN and Confirm PIN are required", null));
            }

            var pinRegex = new Regex(@"^\d{4,6}$");
            if (!pinRegex.IsMatch(transactionPinDto.TransactionPin) || !pinRegex.IsMatch(transactionPinDto.ConfirmTransactionPin))
            {
                return BadRequest(new ApiResponse<string>(false, "Transaction PIN must be a 4-6 digit number", null));
            }

            if (transactionPinDto.TransactionPin != transactionPinDto.ConfirmTransactionPin)
            {
                return BadRequest(new ApiResponse<string>(false, "Transaction PINs do not match", null));
            }

            try
            {
                var userId = GetUserIdFromToken();
                await _userService.SetTransactionPinAsync(userId, transactionPinDto.TransactionPin);
                return Ok(new ApiResponse<string>(true, "Transaction PIN changed successfully", null));
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

        [Authorize]
        [HttpPost("change-pin")]
        public async Task<IActionResult> ChangePin([FromBody] ChangePinDto changePinDto)
        {
            // Validate new PIN and confirm PIN match
            if (changePinDto.NewPin != changePinDto.ConfirmPin)
            {
                return BadRequest(new { status = false, statusMessage = "New PIN and Confirm PIN do not match." });
            }

            // Get user ID from token
            var userId = int.Parse(User.FindFirst("UserId").Value);

            // Attempt to change PIN
            var success = await _userService.ChangePinAsync(userId, changePinDto.CurrentPin, changePinDto.NewPin);

            if (!success)
            {
                return BadRequest(new { status = false, statusMessage = "Failed to change PIN." });
            }

            return Ok(new { status = true, statusMessage = "PIN changed successfully." });
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            // Extract userId from the JWT token claims
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                return Unauthorized(new { status = false, statusMessage = "User ID not found in token" });
            }

            // Parse userId as an integer
            if (!int.TryParse(userIdClaim.Value, out int userId))
            {
                return BadRequest(new { status = false, statusMessage = "Invalid user ID in token" });
            }

            try
            {
                // Fetch user details using userId
                var user = await _userService.GetUserByIdAsync(userId);

                // Map user details to DTO
                var userProfile = new UserProfileDto
                {
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Username = user.Username,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Address = user.Address
                };

                // Return user profile in response
                return Ok(new { status = true, statusMessage = "User profile retrieved successfully", data = userProfile });
            }
            catch (Exception ex)
            {
                // Handle case where user is not found or other exceptions
                return NotFound(new { status = false, statusMessage = ex.Message });
            }



        }
    }

}
