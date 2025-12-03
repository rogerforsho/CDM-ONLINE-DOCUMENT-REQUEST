using System.Security.Cryptography;
using System.Text;

namespace ProjectCapstone.Helpers
{
    public static class SecurityHelper
    {
        // Hash password using PBKDF2
        public static string HashPassword(string password)
        {
            // Generate a random salt
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash the password with the salt
            var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(32);

            // Combine salt and hash
            byte[] hashBytes = new byte[48];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 32);

            // Convert to base64 string
            return Convert.ToBase64String(hashBytes);
        }

        // Verify password against hash
        public static bool VerifyPassword(string password, string hashedPassword)
        {
            try
            {
                if (string.IsNullOrEmpty(hashedPassword)) return false;

                // Extract the bytes
                byte[] hashBytes = Convert.FromBase64String(hashedPassword);

                // Get the salt
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                // Compute the hash on the password the user entered
                var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                byte[] hash = pbkdf2.GetBytes(32);

                // Compare the results
                for (int i = 0; i < 32; i++)
                {
                    if (hashBytes[i + 16] != hash[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Generate random queue number
        public static string GenerateQueueNumber()
        {
            var date = DateTime.Now.ToString("yyyyMMdd");
            var random = new Random().Next(1000, 9999);
            return $"CDM-{date}-{random}";
        }

        // Sanitize input to prevent XSS
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return System.Net.WebUtility.HtmlEncode(input);
        }
    }
}