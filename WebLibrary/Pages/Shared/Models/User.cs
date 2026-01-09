namespace WebLibrary.Pages.Shared.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty; // Hashed
        public string UserType { get; set; } = "学生";
        public string Email { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public int CreditScore { get; set; } = 100;
        public bool IsLimited { get; set; } = false;
    }
}