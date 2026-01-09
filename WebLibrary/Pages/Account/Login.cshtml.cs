using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public LoginModel(IConfiguration cfg) => _cfg = cfg;

        [BindProperty] public string UserName { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(
                "SELECT USER_ID, PASSWORD, USER_TYPE, STATUS " +
                "FROM USERS WHERE USER_NAME = :un", conn);
            cmd.Parameters.Add("un", UserName);

            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                ErrorMessage = "用户不存在";
                return Page();
            }

            if (!PasswordHelper.Verify(Password, r["PASSWORD"].ToString()))
            {
                ErrorMessage = "密码错误";
                return Page();
            }

            if (r["STATUS"].ToString() == "Banned")
            {
                ErrorMessage = "账户被禁用";
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, UserName),
                new("UserId", r["USER_ID"].ToString()!),
                new(ClaimTypes.Role, r["USER_TYPE"].ToString()!),

        };
            await HttpContext.SignInAsync(
                new ClaimsPrincipal(
                    new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return RedirectToPage("/home");
        }
    }
}