
using P2PWallet.Models;
using P2PWallet.Services;

namespace P2PWallet.Services
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}
