using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Utils;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Admin
{
    public class UserDetailsModel : PageModel
    {
        private readonly IConfiguration _config;
        public UserDetailsModel(IConfiguration config) => _config = config;

        public UserDetailsInfo UserInfo { get; set; } = new UserDetailsInfo();
        public void OnGet(int userId)
        {
            LoadUserDetails(userId);
        }

        private void LoadUserDetails(int userId)
        {
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(
                @"SELECT u.User_Name, u.Email, u.User_Type, u.Status
                  FROM Users u
                  WHERE u.user_id = :id", conn);

            cmd.Parameters.Add("id", userId);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                UserInfo = new UserDetailsInfo
                {
                    UserName = reader["User_Name"].ToString(),
                    Email = reader["Email"].ToString(),
                    UserType = reader["User_Type"].ToString(),
                    IsActive = reader["Status"].ToString() == "∆Ù”√",
                };
            }
        }

        public class UserDetailsInfo
        {
            public string UserName { get; set; } = "";
            public string Email { get; set; } = "";
            public string UserType { get; set; } = "student";
            public bool IsActive { get; set; }
        }
    }
}
