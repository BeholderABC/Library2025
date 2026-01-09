using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;

namespace WebLibrary.Pages.Account
{
    public class EditSecurityQuestionsModel : PageModel
    {
        [BindProperty]
        public string? Question { get; set; }
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

        private readonly IConfiguration _c;
        public EditSecurityQuestionsModel(IConfiguration c) => _c = c;

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();
            using var questionCmd = new OracleCommand(
                @"SELECT question_text1, question_text2, question_text3,
                  answer_text1, answer_text2, answer_text3
                  FROM user_answers
                  WHERE user_id = :id", conn);
            questionCmd.Parameters.Add("id", int.Parse(userId!));
            using var reader = questionCmd.ExecuteReader();
            if (reader.Read())
            {
                Question = reader["question_text1"]?.ToString();
                Question2 = reader["question_text2"]?.ToString();
                Question3 = reader["question_text3"]?.ToString();
                Answer1 = reader["answer_text1"]?.ToString();
                Answer2 = reader["answer_text2"]?.ToString();
                Answer3 = reader["answer_text3"]?.ToString();
            }
        }
		public IActionResult OnPostSecurityQuestions()
		{
			var userId = User.FindFirst("UserId")?.Value;

            if ((!string.IsNullOrWhiteSpace(Question) && string.IsNullOrWhiteSpace(Answer1)) ||
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
                if (!string.IsNullOrWhiteSpace(Question)) questionCount++;
                if (!string.IsNullOrWhiteSpace(Question2)) questionCount++;
                if (!string.IsNullOrWhiteSpace(Question3)) questionCount++;

                updateCmd.Parameters.Add("id", userId);
				updateCmd.Parameters.Add("num", questionCount);
				updateCmd.Parameters.Add("q1", string.IsNullOrWhiteSpace(Question) ? DBNull.Value : Question);
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

			return RedirectToPage("/Account/EditSecurityQuestions");
      }
   }
}