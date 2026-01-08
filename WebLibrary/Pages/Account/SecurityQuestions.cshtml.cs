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
        [BindProperty] public string Answer1 { get; set; } = string.Empty;
        [BindProperty] public string Answer2 { get; set; } = string.Empty;

        public string? Question1 { get; set; }
        public string? Question2 { get; set; }
        public void OnGet(string q1, string q2)
        {
            UserId = HttpContext.Session.GetInt32("ResetUserId") ?? 0;
            Question1 = q1;
            Question2 = q2;
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var q1Index = HttpContext.Session.GetInt32("Q1_Index");
            var q2Index = HttpContext.Session.GetInt32("Q2_Index");
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            await conn.OpenAsync();
            using var cmd = new OracleCommand(
            "SELECT answer_text1, answer_text2, answer_text3 " +
            "FROM User_Answers WHERE user_id = :id", conn);
            cmd.Parameters.Add("id", UserId);
            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                string? storedAnswer1 = reader.IsDBNull(q1Index.Value-1) ? null : reader.GetString(q1Index.Value-1);
                string? storedAnswer2 = reader.IsDBNull(q2Index.Value-1) ? null : reader.GetString(q2Index.Value-1);
                if(storedAnswer1 == null ||  storedAnswer2 == null)
                {
                    ModelState.AddModelError("", "密保问题答案为空");
                }
                if ((Answer1 == storedAnswer1) && (Answer2 == storedAnswer2))
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
