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
        [HttpPost("change-pin")]
        public async Task<IActionResult> ChangeTransactionPin([FromBody] TransactionPinDto transactionPinDto)
        {
            // Check if both PINs are provided and match
            if (string.IsNullOrEmpty(transactionPinDto.TransactionPin) || string.IsNullOrEmpty(transactionPinDto.ConfirmTransactionPin))
            {
                return BadRequest(new ApiResponse<string>(false, "Both Transaction PIN and Confirm PIN are required", null));
            }
            // Ensure PINs only contain digits and are of appropriate length (e.g., 4-6 digits)
            var pinRegex = new Regex(@"^\d{4,6}$"); // Adjust the length as needed
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



    }
}
