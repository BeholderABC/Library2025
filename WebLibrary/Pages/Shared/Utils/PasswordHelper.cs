using System.Security.Cryptography;
using System.Text;

namespace WebLibrary.Pages.Shared.Utils
{
    public static class PasswordHelper
    {
        public static string HashPassword(string pwd) =>
            Convert.ToBase64String(SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(pwd)));

        public static bool Verify(string input, string hash) => HashPassword(input) == hash;
        
        public static bool VerifyAnswer(string inputAnswer, string storedAnswer)
        {
            return HashPassword(inputAnswer) == storedAnswer;
        }
    }
}
