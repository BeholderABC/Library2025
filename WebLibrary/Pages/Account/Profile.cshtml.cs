using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;

namespace WebLibrary.Pages.Account
{
    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly IConfiguration _c;
        public ProfileModel(IConfiguration c) => _c = c;
        public User UserInfo { get; set; } = new();

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = new OracleCommand("SELECT * FROM Users WHERE user_id=:id", conn);
            cmd.Parameters.Add("id", int.Parse(userId!));
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                UserInfo = new User
                {
                    UserName = r["user_name"].ToString()!,
                    Email = r["email"].ToString()!,
                    UserType = r["user_type"].ToString()!,
                    Status = r["status"].ToString()!,
                    CreditScore = Convert.ToInt32(r["credit_score"]),
                    IsLimited = Convert.ToBoolean(r["is_limited"])
                };
            }
        }
    }
}
