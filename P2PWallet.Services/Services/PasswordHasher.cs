using System;
using System.Security.Cryptography;
using System.Text;
using P2PWallet.Models;
using P2PWallet.Services;




namespace P2PWallet.Services
{
    public static class PasswordHasher
    {
        public static (string hash, byte[] salt) HashPassword(string password)
        {
            // Generate a 128-bit (16-byte) salt
            byte[] saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            string salt = Convert.ToBase64String(saltBytes);

            // Hash the password with the salt
            using (var hmac = new HMACSHA512(saltBytes))
            {
                byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
                byte[] hashBytes = hmac.ComputeHash(passwordBytes);
                string hash = Convert.ToBase64String(hashBytes);
                return (hash, saltBytes);
            }
        }

        public static bool VerifyPassword(string password, string hash, string salt)
        {
            // Convert the salt back to bytes
            byte[] saltBytes = Convert.FromBase64String(salt);

            // Rehash the input password with the stored salt
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

    public class HashingService
    {
        public static (string hashedPin, byte[] salt) HashPin(string pin)
        {
            // Generate a new salt
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var hmac = new HMACSHA512(salt))
            {
                var hashedPin = hmac.ComputeHash(Encoding.UTF8.GetBytes(pin));
                return (Convert.ToBase64String(hashedPin), salt);
            }
        }

        public static bool VerifyPin(string enteredPin, string storedHashedPin, byte[] storedSalt)
        {
            using (var hmac = new HMACSHA512(storedSalt))
            {
                var hashedEnteredPin = hmac.ComputeHash(Encoding.UTF8.GetBytes(enteredPin));
                return Convert.ToBase64String(hashedEnteredPin) == storedHashedPin;
            }
        }
    }
}
