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
        [BindProperty] public string? Phone { get; set; }
        [BindProperty] public string? NewPassword { get; set; }
        [BindProperty] public string? ConfirmPassword { get; set; }
        [BindProperty]
        public string? Question1 { get; set; }
        [BindProperty]
        public string? Question2 { get; set; }
        [BindProperty]
        public string? Question3 { get; set; }
        [BindProperty]
        public string? Answer1 { get; set; }
        [BindProperty]
        public string? Answer2 { get; set; }
        [BindProperty]
        public string? Answer3 { get; set; }


        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = new OracleCommand(
                "SELECT user_name, email, phone FROM Users WHERE user_id=:id", conn);
            cmd.Parameters.Add("id", int.Parse(userId!));
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                UserName = r["user_name"].ToString()!;
                Email = r["email"] == DBNull.Value ? null : r["email"].ToString();
                Phone = r["phone"] == DBNull.Value ? null : r["phone"].ToString();
            }

            using var questionCmd = new OracleCommand(
               @"SELECT question_text1, question_text2, question_text3,
                  answer_text1, answer_text2, answer_text3
                  FROM user_answers
                  WHERE user_id = :id", conn);
            questionCmd.Parameters.Add("id", int.Parse(userId!));
            using var reader = questionCmd.ExecuteReader();
            if (reader.Read())
            {
                Question1 = reader["question_text1"]?.ToString();
                Question2 = reader["question_text2"]?.ToString();
                Question3 = reader["question_text3"]?.ToString();
                Answer1 = reader["answer_text1"]?.ToString();
                Answer2 = reader["answer_text2"]?.ToString();
                Answer3 = reader["answer_text3"]?.ToString();
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

            if (!string.IsNullOrWhiteSpace(Phone) &&
                DbUniqueChecker.Exists(_c, "PHONE", Phone, excludeId: userId))
            {
                ModelState.AddModelError(nameof(Phone), "手机号已被占用");
                return Page();
            }

            // 3. 保存
            using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(@"
            UPDATE Users
            SET user_name = :un,
                email     = :em,
                phone     = :ph
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
            cmd.Parameters.Add("ph", string.IsNullOrWhiteSpace(Phone) ? DBNull.Value : Phone);
            cmd.Parameters.Add("id", userId);

            cmd.ExecuteNonQuery();
            return RedirectToPage("/Account/Profile");
        }

        public IActionResult OnPostSecurityQuestions()
        {
            var userId = User.FindFirst("UserId")?.Value;

            if ((!string.IsNullOrWhiteSpace(Question1) && string.IsNullOrWhiteSpace(Answer1)) ||
                (!string.IsNullOrWhiteSpace(Question2) && string.IsNullOrWhiteSpace(Answer2)) ||
                (!string.IsNullOrWhiteSpace(Question3) && string.IsNullOrWhiteSpace(Answer3)))
            {
                TempData["ErrorMessage"] = "有的问题没有填写答案";
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
						question_text1 = :q1, answer_text1 = :a1,
						question_text2 = :q2, answer_text2 = :a2,
						question_text3 = :q3, answer_text3 = :a3
					WHERE user_id = :id", conn);
                }
                else
                {
                    updateCmd = new OracleCommand(@"
					INSERT INTO user_answers(
						user_id, question_num,
						question_text1, answer_text1,
						question_text2, answer_text2,
						question_text3, answer_text3
					)
					VALUES (:id, :num, :q1, :a1, :q2, :a2, :q3, :a3)", conn);
                }
                updateCmd.BindByName = true;
                int questionCount = 0;
                if (!string.IsNullOrWhiteSpace(Question1)) questionCount++;
                if (!string.IsNullOrWhiteSpace(Question2)) questionCount++;
                if (!string.IsNullOrWhiteSpace(Question3)) questionCount++;

                updateCmd.Parameters.Add("id", userId);
                updateCmd.Parameters.Add("num", questionCount);
                updateCmd.Parameters.Add("q1", string.IsNullOrWhiteSpace(Question1) ? DBNull.Value : Question1);
                updateCmd.Parameters.Add("a1", string.IsNullOrWhiteSpace(Answer1) ? DBNull.Value : Answer1);
                updateCmd.Parameters.Add("q2", string.IsNullOrWhiteSpace(Question2) ? DBNull.Value : Question2);
                updateCmd.Parameters.Add("a2", string.IsNullOrWhiteSpace(Answer2) ? DBNull.Value : Answer2);
                updateCmd.Parameters.Add("q3", string.IsNullOrWhiteSpace(Question3) ? DBNull.Value : Question3);
                updateCmd.Parameters.Add("a3", string.IsNullOrWhiteSpace(Answer3) ? DBNull.Value : Answer3);

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