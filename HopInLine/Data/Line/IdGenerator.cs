namespace HopInLine.Data.Line
{
    using System.Security.Cryptography;

    public static class IdGenerator
    {
        // Unambiguous alphabet: no 0/O, no 1/I/L
        private const string UnambiguousAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        /// <summary>
        /// Generates an 8-character unambiguous Base32 join code using crypto-random selection.
        /// </summary>
        public static string GenerateUniqueId()
        {
            const int idLength = 8;
            const int alphabetSize = 31; // UnambiguousAlphabet.Length
            var idChars = new char[idLength];

            for (int i = 0; i < idLength; i++)
            {
                // Use rejection sampling to avoid bias with modulo
                int randomValue;
                do
                {
                    randomValue = RandomNumberGenerator.GetInt32(256);
                } while (randomValue >= 256 - (256 % alphabetSize));

                idChars[i] = UnambiguousAlphabet[randomValue % alphabetSize];
            }

            return new string(idChars);
        }

        /// <summary>
        /// Generates a 128-bit URL-safe Base64 secret without padding.
        /// </summary>
        public static string GenerateManageToken()
        {
            const int tokenBytes = 16; // 128 bits
            var tokenData = new byte[tokenBytes];
            RandomNumberGenerator.Fill(tokenData);

            // Convert to URL-safe Base64 without padding
            string base64 = Convert.ToBase64String(tokenData);
            // Replace + with -, / with _, and strip padding (=)
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        /// <summary>
        /// Normalizes a join code: returns empty string for null/whitespace,
        /// otherwise returns trimmed and upper-cased input.
        /// </summary>
        public static string NormalizeId(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return input.Trim().ToUpperInvariant();
        }
    }
}
