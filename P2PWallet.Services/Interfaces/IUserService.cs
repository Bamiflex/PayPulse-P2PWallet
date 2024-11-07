
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

        Task CreateTransactionAsync(Transaction transaction);

        Task AddBalance(int userId, decimal amount);

        /// <summary>
        /// Updates the status of a transaction.
        /// </summary>
        /// <param name="transactionReference">The reference of the transaction to update.</param>
        /// <param name="status">The new status of the transaction.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpdateTransactionStatus(string transactionReference, string status);

        //Task<User> GetUserByEmailAsync(string email);
        //Task UpdateUserAsync(User user);


    }
}
