
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using P2PWallet.Models;
using P2PWallet.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static P2PWallet.Models.Models.PaystackVerificationResponse;

namespace P2PWallet.Services
{
    public class UserService : IUserService
    {
        private readonly ITokenService _tokenService;
        private readonly P2PWalletDbContext _dbContext;
        private readonly P2PWalletDbContext _context;


        public UserService(ITokenService tokenService, P2PWalletDbContext dbContext, P2PWalletDbContext context)
        {
            _tokenService = tokenService;
            _dbContext = dbContext;
            _context = context;
        }

        public async Task<User> RegisterUserAsync(UserRegistrationDto userDto)
        {
            
            if (_dbContext.Users.Any(u => u.Email == userDto.Email || u.Username == userDto.Username))
            {
                throw new InvalidOperationException("Email or Username already exists.");
            }

            
            var (passwordHash, passwordSalt) = PasswordHasher.HashPassword(userDto.Password);

            
            byte[] pinSalt = new byte[16]; 
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(pinSalt);
            }

            
            var (transactionPinHash, _) = HashingService.HashPin("0000");

            
            var user = new User
            {
                Username = userDto.Username,
                Email = userDto.Email,
                PhoneNumber = userDto.PhoneNumber,
                FirstName = userDto.FirstName,
                LastName = userDto.LastName,
                Address = userDto.Address,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                PinSalt = pinSalt, 
                TransactionPinHash = transactionPinHash 
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            
            user.Account = await CreateAccountAsync(user.Id);

            return user;
        }


        public async Task CreateTransactionAsync(Transaction transaction)
        {
            _dbContext.Transactions.Add(transaction);
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateTransactionStatus(string transactionReference, string status)
        {
            var transaction = await _dbContext.Transactions
                .FirstOrDefaultAsync(t => t.ExternalTransactionId == transactionReference);

            if (transaction == null)
            {
                throw new InvalidOperationException("Transaction not found.");
            }

            transaction.Status = status;

            await _dbContext.SaveChangesAsync();
        }


        private (byte[] passwordHash, byte[] passwordSalt) HashPassword(string password)
        {
            using (var hmac = new HMACSHA512())
            {
                var salt = hmac.Key;
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                return (hash, salt);
            }
        }



        public async Task<Account> CreateAccountAsync(int userId)
        {
            string accountNumber = GenerateUniqueAccountNumber();

            var account = new Account
            {
                AccountNumber = accountNumber,
                Balance = 10000M,
                Currency = "NGN",
                UserId = userId
            };

            _dbContext.Accounts.Add(account);
            await _dbContext.SaveChangesAsync();

            return account;
        }

        public async Task<bool> SetTransactionPinAsync(int userId, string transactionPin)


        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            var (hashedPin, salt) = HashingService.HashPin(transactionPin);
            user.TransactionPinHash = hashedPin;
            user.PinSalt = salt;

            await _dbContext.SaveChangesAsync();
            return true;
        }


        public async Task<string> GetAccountNameByAccountNumberAsync(string accountNumber)
        {
            var account = await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);

            return account?.User != null ? $"{account.User.FirstName} {account.User.LastName}" : null;
        }
        public async Task<bool> TransferFundsAsync(string fromAccount, string toAccount, decimal amount, string transactionPin)
        {
            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var senderAccount = await _dbContext.Accounts
                        .Include(a => a.User)
                        .FirstOrDefaultAsync(a => a.AccountNumber == fromAccount);
                    var recipientAccount = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == toAccount);

                    if (senderAccount == null || recipientAccount == null)
                        throw new InvalidOperationException("Invalid account number(s).");

                    if (senderAccount.Balance < amount)
                        throw new InvalidOperationException("Insufficient balance.");

                    var user = senderAccount.User;
                    if (user == null || !HashingService.VerifyPin(transactionPin, user.TransactionPinHash, user.PinSalt))
                        throw new InvalidOperationException("Invalid transaction PIN.");

                    // Deduct from sender and add to recipient
                    senderAccount.Balance -= amount;
                    recipientAccount.Balance += amount;

                    // Create transaction for sender
                    var senderTransaction = new Transaction
                    {
                        AccountId = senderAccount.AccountId,
                        AccountNumber = fromAccount,
                        Date = DateTime.UtcNow,
                        Amount = amount,
                        Type = TransactionType.Debit,
                        BalanceAfterTransaction = senderAccount.Balance,
                        Description = $"Transfer to {toAccount}",
                        Status = "Success",
                        ExternalTransactionId = GenerateExternalTransactionId()
                    };

                    // Create transaction for recipient
                    var receiverTransaction = new Transaction
                    {
                        AccountId = recipientAccount.AccountId,
                        AccountNumber = toAccount,
                        Date = DateTime.UtcNow,
                        Amount = amount,
                        Type = TransactionType.Credit,
                        BalanceAfterTransaction = recipientAccount.Balance,
                        Description = $"Transfer from {fromAccount}",
                        Status = "Success",
                        ExternalTransactionId = GenerateExternalTransactionId()
                    };

                    _dbContext.Transactions.Add(senderTransaction);
                    _dbContext.Transactions.Add(receiverTransaction);
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        // Helper method to generate an external transaction ID
        private string GenerateExternalTransactionId()
        {
            return Guid.NewGuid().ToString();
        }

        public async Task AddBalance(int userId, decimal amount)
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId);

            if (account != null)
            {
                account.Balance += amount;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<decimal> GetBalanceAsync(string accountNumber)
        {
            var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
            if (account == null) throw new InvalidOperationException("Account not found.");
            return account.Balance;
        }

        private string GenerateUniqueAccountNumber()
        {
            var random = new Random();
            string accountNumber;

            do
            {
                int randomNumber = random.Next(1000000, 10000000);
                accountNumber = "200" + randomNumber.ToString();
            } while (_dbContext.Accounts.Any(a => a.AccountNumber == accountNumber));

            return accountNumber;
        }




        public async Task<LoginResultDto> LoginUserAsync(string username, string password)
        {
            var user = await _dbContext.Users.Include(u => u.Account)
                                              .FirstOrDefaultAsync(u => u.Username == username);
                if (user == null || !PasswordHasher.VerifyPassword(password, user.PasswordHash, Convert.ToBase64String(user.PasswordSalt)))
            {
                throw new InvalidOperationException("Invalid username or password.");
            }

            var token = _tokenService.GenerateToken(user);


            return new LoginResultDto
            {
                Token = token,
                AccountNumber = user.Account.AccountNumber,
                Balance = user.Account.Balance
            };
        }

      

        public static bool VerifyPassword(string password, string hash, string salt)
        {
            byte[] saltBytes = Convert.FromBase64String(salt);

            using (var hmac = new HMACSHA512(saltBytes))
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = hmac.ComputeHash(passwordBytes);
                string computedHash = Convert.ToBase64String(hashBytes);

                // Compare the computed hash with the stored hash
                return computedHash == hash;
            }
        }

    }
}

