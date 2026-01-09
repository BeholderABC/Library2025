using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;

namespace WebLibrary.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly IConfiguration _config;
        public ForgotPasswordModel(IConfiguration config) => _config = config;

        [BindProperty] public string UserName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // 验证用户名是否存在
                using var cmd = new OracleCommand(
                    "SELECT user_id FROM Users WHERE User_Name = :username", conn);
                cmd.Parameters.Add("username", UserName);
                var userId2 = (long?)await cmd.ExecuteScalarAsync();
                int? userId = userId2.HasValue ? (int?)userId2.Value : null;
                if (userId == null)
                {
                    ErrorMessage = "用户不存在";
                    return Page();
                }

                // 查询安全问题
                using var questioncmd = new OracleCommand(
                    "SELECT question_num, question_text1, question_text2,question_text3 FROM User_Answers WHERE user_id = :id", conn);
                questioncmd.Parameters.Add("id", userId.Value);

                var questions = new List<(string Text, int Index)>();
                using var reader = await questioncmd.ExecuteReaderAsync();

                if (await reader.ReadAsync()) 
                {
                    int questionNum = reader.GetInt32(0);
                    if (questionNum < 1)
                    {
                        ErrorMessage = "未设置安全问题，无法通过安全问题重置密码，请联系管理员协助";
                        return Page();
                    }
                    else
                    {
                        int validQuetionsCount = 0;
                        for (int i = 1; i <= questionNum; i++)
                        {
                            string? questionText = reader.IsDBNull(i) ? null : reader.GetString(i);
                            if (!string.IsNullOrEmpty(questionText)) 
                            {
                                questions.Add((questionText, i));
                                validQuetionsCount++;
                            }
                        }

                        if(validQuetionsCount < 1)
                        {
                            ErrorMessage = "未设置安全问题，无法通过安全问题重置密码，请联系管理员协助";
                            return Page();
                        }
                        HttpContext.Session.SetInt32("ResetUserId", userId.Value);
                        HttpContext.Session.SetInt32("Q1_Index", questions[0].Index);
                        return RedirectToPage("SecurityQuestions", new
                        {
                            q1 = questions[0].Text
                        });
                    }
                }
                else
                {
                    ErrorMessage = "查询安全问题失败";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                return Page();
            }
        }
    }
}
