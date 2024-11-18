
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
                TransactionPinHash = transactionPinHash,
                IsDefaultPin = true
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
                Balance = 0M,
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

            // Hash the provided transaction PIN
            var (hashedPin, salt) = HashingService.HashPin(transactionPin);
            user.TransactionPinHash = hashedPin;
            user.PinSalt = salt;

            user.IsDefaultPin = false;

            Console.WriteLine($"IsDefaultPin before save: {user.IsDefaultPin}");

            // Save changes to the database
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

        public async Task<LoginResult> LoginUserAsync(string username, string password)
        {
            var user = await _dbContext.Users.Include(u => u.Account)
                                              .FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || !PasswordHasher.VerifyPassword(password, user.PasswordHash, Convert.ToBase64String(user.PasswordSalt)))
            {
                throw new InvalidOperationException("Invalid username or password.");
            }

            // Check if the user needs to set a new PIN based on IsDefaultPin
            bool needsPin = user.IsDefaultPin;

            // Generate token
            var token = _tokenService.GenerateToken(user);

            return new LoginResult
            {
                Token = token,
                AccountNumber = user.Account.AccountNumber,
                Balance = user.Account.Balance,
                NeedsPin = needsPin
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

                return computedHash == hash;
            }
        }

        public async Task<bool> ChangePinAsync(int userId, string currentPin, string newPin)
        {
            // Step 1: Retrieve the user
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            // Step 2: Verify the current PIN
            using (var hmac = new HMACSHA512(user.PinSalt))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(currentPin));
                for (int i = 0; i < computedHash.Length; i++)
                {
                    if (computedHash[i] != Convert.FromBase64String(user.TransactionPinHash)[i])
                    {
                        throw new Exception("Current PIN is incorrect");
                    }
                }
            }

            // Step 3: Generate a new PIN hash and salt
            using (var hmac = new HMACSHA512())
            {
                user.PinSalt = hmac.Key;
                var newPinHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(newPin));
                user.TransactionPinHash = Convert.ToBase64String(newPinHash);  // Convert byte[] to Base64 string
            }

            // Step 4: Save the changes to the database
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Transaction>> GetCreditTransactionsAsync(int userId)
        {
            return await _context.Transactions
                                 .Where(t => t.AccountId == userId && t.Type == TransactionType.Credit)
                                 .OrderByDescending(t => t.Date)
                                 .ToListAsync();
        }

        public async Task<List<Transaction>> GetDebitTransactionsAsync(int userId)
        {
            return await _context.Transactions
                                 .Where(t => t.AccountId == userId && t.Type == TransactionType.Debit)
                                 .OrderByDescending(t => t.Date)
                                 .ToListAsync();
        }


        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            // Verify the current password
            using (var hmac = new HMACSHA512(user.PasswordSalt))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(currentPassword));
                var computedHashString = Convert.ToBase64String(computedHash); // Convert to string for comparison

                if (computedHashString != user.PasswordHash)
                {
                    throw new Exception("Current password is incorrect");
                }
            }

            // Hash the new password and convert to a string
            using (var hmac = new HMACSHA512())
            {
                user.PasswordSalt = hmac.Key;
                var newHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(newPassword));
                user.PasswordHash = Convert.ToBase64String(newHash); // Convert to string for storage
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<User> GetUserByIdAsync(int userId)
        {
            if (userId <= 0)
            {
                throw new ArgumentException("Invalid user ID provided.");
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found.");
            }

            return user;
        }


    }
}

