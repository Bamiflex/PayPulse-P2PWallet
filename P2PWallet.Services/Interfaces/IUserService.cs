
using P2PWallet.Models;
using P2PWallet.Services;


namespace P2PWallet.Services
{
    public interface IUserService
    {
        Task<User> RegisterUserAsync(UserRegistrationDto userDto);
        Task<Account> CreateAccountAsync(int userId);
        Task<bool> TransferFundsAsync(string fromAccount, string toAccount, decimal amount, string transactionPin);
        Task<decimal> GetBalanceAsync(string accountNumber);
        Task<bool> SetTransactionPinAsync(int userId, string TransactionPin);
        Task<LoginResultDto> LoginUserAsync(string username, string password);
        Task<string> GetAccountNameByAccountNumberAsync(string accountNumber);

        Task AddBalance(int userId, decimal amount);

    }
}
