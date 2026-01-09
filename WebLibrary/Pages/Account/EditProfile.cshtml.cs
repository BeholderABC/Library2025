using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Account
{
    [Authorize]
    public class EditProfileModel : PageModel
    {
        private readonly IConfiguration _c;
        public EditProfileModel(IConfiguration c) => _c = c;

        [BindProperty] public string UserName { get; set; } = "";
        [BindProperty] public string? Email { get; set; }
        [BindProperty] public string? NewPassword { get; set; }
        [BindProperty] public string? ConfirmPassword { get; set; }
        [BindProperty]
        public string? Question { get; set; }
        [BindProperty]
        public string? Answer { get; set; }



        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = new OracleCommand(
                "SELECT user_name, email, FROM Users WHERE user_id=:id", conn);
            cmd.Parameters.Add("id", int.Parse(userId!));
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                UserName = r["user_name"].ToString()!;
                Email = r["email"] == DBNull.Value ? null : r["email"].ToString();
            }

            using var questionCmd = new OracleCommand(
               @"SELECT question_text1, 
                  answer_text1
                  FROM user_answers
                  WHERE user_id = :id", conn);
            questionCmd.Parameters.Add("id", int.Parse(userId!));
            using var reader = questionCmd.ExecuteReader();
            if (reader.Read())
            {
                Question = reader["question_text1"]?.ToString();
                Answer = reader["answer_text1"]?.ToString();
            }
        }

        public IActionResult OnPostUserProfile()
        {
            var userIdStr = User.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            // 1. 基础校验
            if (!string.IsNullOrWhiteSpace(NewPassword) && NewPassword != ConfirmPassword)
            {
                ModelState.AddModelError(nameof(NewPassword), "两次新密码不一致");
                return Page();
            }

            // 2. 唯一性校验 
            if (DbUniqueChecker.Exists(_c, "USER_NAME", UserName, excludeId: userId))
            {
                ModelState.AddModelError(nameof(UserName), "用户名已被占用");
                return Page();
            }

            if (!string.IsNullOrWhiteSpace(Email) &&
                DbUniqueChecker.Exists(_c, "EMAIL", Email, excludeId: userId))
            {
                ModelState.AddModelError(nameof(Email), "邮箱已被占用");
                return Page();
            }

            // 3. 保存
            using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(@"
            UPDATE Users
            SET user_name = :un,
                email     = :em,
                {0}
            WHERE user_id = :id", conn);

            cmd.BindByName = true;
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                cmd.CommandText = string.Format(cmd.CommandText, ", password = :pw");
                cmd.Parameters.Add("pw", PasswordHelper.HashPassword(NewPassword));
            }
            else
            {
                cmd.CommandText = string.Format(cmd.CommandText, "");
            }

            cmd.Parameters.Add("un", UserName);
            cmd.Parameters.Add("em", string.IsNullOrWhiteSpace(Email) ? DBNull.Value : Email);
            cmd.Parameters.Add("id", userId);

            cmd.ExecuteNonQuery();
            return RedirectToPage("/Account/Profile");
        }

        public IActionResult OnPostSecurityQuestions()
        {
            var userId = User.FindFirst("UserId")?.Value;

            if ((!string.IsNullOrWhiteSpace(Question) && string.IsNullOrWhiteSpace(Answer)))
            {
                TempData["ErrorMessage"] = "密保问题没有填写答案";
                return RedirectToPage("/Account/EditSecurityQuestions");
            }

            try
            {
                using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
                conn.Open();
                using var CheckCmd = new OracleCommand(
                    "SELECT COUNT(*) FROM user_answers WHERE user_id = :id", conn);
                CheckCmd.Parameters.Add("id", userId);
                var exists = Convert.ToInt32(CheckCmd.ExecuteScalar()) > 0;
                OracleCommand updateCmd;
                if (exists)
                {
                    updateCmd = new OracleCommand(@"
					UPDATE user_answers SET
						question_num = :num,
						question_text1 = :q1, answer_text1 = :a1
					WHERE user_id = :id", conn);
                }
                else
                {
                    updateCmd = new OracleCommand(@"
					INSERT INTO user_answers(
						user_id, question_num,
						question_text1, answer_text1,
					)
					VALUES (:id, :num, :q1, :a1)", conn);
                }
                updateCmd.BindByName = true;
                int questionCount = 0;
                if (!string.IsNullOrWhiteSpace(Question)) questionCount++;

                updateCmd.Parameters.Add("id", userId);
                updateCmd.Parameters.Add("num", questionCount);
                updateCmd.Parameters.Add("q1", string.IsNullOrWhiteSpace(Question) ? DBNull.Value : Question);
                updateCmd.Parameters.Add("a1", string.IsNullOrWhiteSpace(Answer) ? DBNull.Value : Answer);

                updateCmd.ExecuteNonQuery();
                TempData["SuccessMessage"] = "密保问题已修改";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"出现了错误：{ex.Message}";
            }

            return RedirectToPage("/Account/EditProfile");
        }

    }
}