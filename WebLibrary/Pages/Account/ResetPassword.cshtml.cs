using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Utils;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private IConfiguration _config;
        public ResetPasswordModel(IConfiguration config)
        {
            _config = config;
        }

        [BindProperty] public int UserId { get; set; }
        [BindProperty] public string? NewPassword { get; set; }
        [BindProperty] public string? ConfirmPassword { get; set; }
        public void OnGet()
        {
			UserId = HttpContext.Session.GetInt32("ResetUserId") ?? 0;
		}

        public async Task<IActionResult> OnPostAsync()
        {
            if(NewPassword != ConfirmPassword)
            {
                ModelState.AddModelError("", "两次输入的密码不一致");
                return Page();
            }
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            await conn.OpenAsync();

            using var cmd = new OracleCommand(
                "UPDATE Users SET password = :pwd " +
                "WHERE User_Id = :id", conn);

            cmd.Parameters.Add("pwd", PasswordHelper.HashPassword(NewPassword));
            cmd.Parameters.Add("id", UserId);

            await cmd.ExecuteNonQueryAsync();

            HttpContext.Session.Remove("ResetUserId");
            return RedirectToPage("/Account/Login");
        }
    }
}
