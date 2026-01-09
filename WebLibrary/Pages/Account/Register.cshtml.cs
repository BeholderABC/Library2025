using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public RegisterModel(IConfiguration cfg) => _cfg = cfg;

        [BindProperty] public string UserName { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";
        [BindProperty] public string ConfirmPassword { get; set; } = "";
        [BindProperty] public string UserType { get; set; } = "读者";
        [BindProperty] public string? Email { get; set; }

        public string? ErrorMessage { get; set; }

        public IActionResult OnPost()
        {
            // 1. 简单校验
            if (string.IsNullOrWhiteSpace(UserName) || UserName.Length > 20)
            {
                ErrorMessage = "用户名为 1-20 位字符";
                return Page();
            }
            if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
            {
                ErrorMessage = "密码至少 6 位";
                return Page();
            }
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "两次密码不一致";
                return Page();
            }

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            // 2. 唯一性检查
            if (DbUniqueChecker.Exists(_cfg, "USER_NAME", UserName))
            {
                ErrorMessage = "用户名已存在";
                return Page();
            }
            if (!string.IsNullOrWhiteSpace(Email) &&
                DbUniqueChecker.Exists(_cfg, "EMAIL", Email))
            {
                ErrorMessage = "邮箱已被注册";
                return Page();
            }
            

            // 3. 插入
            using var cmd = new OracleCommand(
                @"INSERT INTO USERS
                  (USER_ID, USER_NAME, PASSWORD, USER_TYPE, EMAIL)
                  VALUES
                  (USER_SEQ.NEXTVAL, :un, :pw, :ut, :em)", conn);

            cmd.Parameters.Add("un", UserName);
            cmd.Parameters.Add("pw", PasswordHelper.HashPassword(Password));
            cmd.Parameters.Add("ut", UserType);
            cmd.Parameters.Add("em", (object?)Email ?? DBNull.Value);

            cmd.ExecuteNonQuery();
            return RedirectToPage("/Account/Login");
        }
    }
}