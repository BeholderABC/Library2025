using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Account
{
    public class SecurityQuestionsModel : PageModel
    {
        private readonly IConfiguration _config;
        public SecurityQuestionsModel(IConfiguration config) => _config = config;
        public string ErrorMessage { get; set; } = string.Empty;

        [BindProperty] public int UserId { get; set; }
        [BindProperty] public string Answer { get; set; } = string.Empty;

        public string? Question { get; set; }
        public void OnGet(string question)
        {
            UserId = HttpContext.Session.GetInt32("ResetUserId") ?? 0;
            Question = question;
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var q1Index = HttpContext.Session.GetInt32("Q1_Index");
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            await conn.OpenAsync();
            using var cmd = new OracleCommand(
            "SELECT answer_text" +
            "FROM User_Answers WHERE user_id = :id", conn);
            cmd.Parameters.Add("id", UserId);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                string? storedAnswer1 = reader.IsDBNull(q1Index.Value-1) ? null : reader.GetString(q1Index.Value-1);
                if(storedAnswer1 == null)
                {
                    ModelState.AddModelError("", "密保问题答案为空");
                }
                if (Answer == storedAnswer1)
                {
                    // 答案正确，进入密码重置页面
                    return RedirectToPage("ResetPassword");
                }
            }

            ErrorMessage = "密保答案不正确";
            ModelState.AddModelError("", "密保答案不正确");
            return Page();
        }
    }
}
