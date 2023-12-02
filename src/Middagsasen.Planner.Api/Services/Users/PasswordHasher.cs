using Konscious.Security.Cryptography;
using System.Security.Cryptography;
using System.Text;

namespace Middagsasen.Planner.Api.Services.Users
{
    public class PasswordHasher
    {
        public static byte[] CreateSalt()
        {
            var buffer = RandomNumberGenerator.GetBytes(32);
            return buffer;
        }

        public static byte[] HashPassword(string password, byte[] salt)
        {
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = 4, // 2 cores
                Iterations = 2,
                MemorySize = 64 * 1024 // .5 GB
            };

            return argon2.GetBytes(32);
        }

        public static bool VerifyHash(string password, byte[] salt, byte[] hash)
        {
            var newHash = HashPassword(password, salt);
            return hash.SequenceEqual(newHash);
        }
    }
}
