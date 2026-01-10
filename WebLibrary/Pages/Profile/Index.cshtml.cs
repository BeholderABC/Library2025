using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
// Scroll
using Scroll.Models;
using Scroll.Services;
// Account
using Microsoft.AspNetCore.Authorization;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;
using DocumentFormat.OpenXml.Spreadsheet;
using WebLibrary.Pages.Shared.Utils;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.EMMA;


namespace WebLibrary.Pages.Profile
{
    using UserType = Decimal;
    using CateNameType = String;

    [Authorize]
    public class ProfileModel : PageModel
    {
        private readonly ICurated _curated;
        private readonly IPreference _preference;
        private readonly ITop _top;
        private readonly IConfiguration _conf;

        // Common
        public UserType uid;
        // Cover
        public Int64 UserBorrowCount { get; set; }
        public Int64 UserCommentCount { get; set; }
        // Recommend
        public List<BookList>? RecommendedBooks { get; set; }
        // History
        public List<BorrowList>? HistoryBooks { get; set; }
        public Dictionary<CateNameType, Int64> InterestCates { get; set; }
        // Reviews
        public List<CommentInfo> UserComments { get; set; } = new();
        // Info
        [BindProperty] public User UserInfo { get; set; }
        [BindProperty] public string? NewPassword { get; set; }
        [BindProperty] public string? ConfirmPassword { get; set; }
        // [BindProperty] public List<string> Qs { get; set; }
        // [BindProperty] public List<string> As { get; set; }
        [BindProperty] public QAs qas { get; set; }

        public ProfileModel(ICurated curated, IPreference preference, ITop top, IConfiguration conf)
        {
            // Recommend
            _curated = curated;
            _preference = preference;
            _top = top;

            // Info
            _conf = conf;
            UserInfo = new User();
        }

        public void OnGet()
        {
            // Common
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var userId))
                //return Unauthorized();
                return;
            uid = userId;

            // Info
            UserInfo = GetUserProfile();
            qas = GetSecurityQAs();


            // Recommend
            //RecommendedBooks = _curated.CuratedList(uid);
            //RecommendedBooks = _top.TopList();
            //HistoryBooks = _preference.History(uid);
            //InterestCates = _preference.Interest(uid);
            //Reviews
            //UserComments = GetUserComments(uid);
            // Cover
            //UserBorrowCount = HistoryBooks.Count;
            //UserCommentCount = UserComments.Count;

        }

        public class CommentInfo
        {
            public string Content { get; set; } = "";
            public int Rating { get; set; }
            public DateTime CommentDate { get; set; }
            public string BookTitle { get; set; } = "";
        }

        // 添加获取评论数据的方法
        private List<CommentInfo> GetUserComments(decimal userId)
        {
            var comments = new List<CommentInfo>();

            try
            {
                using var conn = new OracleConnection(_conf.GetConnectionString("OracleDb"));
                conn.Open();

                using var cmd = new OracleCommand(@"
            SELECT c.content, c.rating, c.comment_date, 
                   b.title
            FROM comments c
            JOIN book b ON c.book_id = b.book_id
            WHERE c.user_id = :user_id
            ORDER BY c.comment_date DESC", conn);

                cmd.Parameters.Add("user_id", OracleDbType.Int32).Value = userId;

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    comments.Add(new CommentInfo
                    {
                        Content = reader["content"] == DBNull.Value ? "" : reader["content"].ToString(),
                        Rating = Convert.ToInt32(reader["rating"]),
                        CommentDate = Convert.ToDateTime(reader["comment_date"]),
                        BookTitle = reader["title"] == DBNull.Value ? "无标题" : reader["title"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                // 记录错误，但继续执行
                Console.WriteLine($"获取用户评论时出错: {ex.Message}");
            }

            return comments;
        }

        // 更新个人信息的AJAX处理器
        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var userId))
                return Unauthorized();

            // 基本验证
            if (string.IsNullOrWhiteSpace(UserInfo.UserName))
            {
                return BadRequest("用户名不能为空");
            }

            // 唯一性检查
            if (!ValidateUniqueness(userId))
            {
                return BadRequest("用户名、邮箱或手机号已被占用");
            }

            // 更新个人信息
            UpdateUserProfile(userId);
            TempData["SuccessMessage"] = "个人信息更新成功！";

            return new OkResult();
        }

        // 更新密码的AJAX处理器
        public async Task<IActionResult> OnPostUpdatePasswordAsync()
        {
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var userId))
                return Unauthorized();

            // 密码验证
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                return BadRequest("请输入新密码");
            }

            if (NewPassword.Length < 6)
            {
                return BadRequest("密码长度不能少于6位");
            }

            if (NewPassword != ConfirmPassword)
            {
                return BadRequest("两次密码输入不一致");
            }

            // 更新密码
            UpdatePassword(userId);
            TempData["SuccessMessage"] = "密码更新成功！";

            return new OkResult();
        }

        // 更新密保问题的AJAX处理器
        public async Task<IActionResult> OnPostUpdateSecurityAsync()
        {
            if (!int.TryParse(User.FindFirst("UserId")?.Value, out var userId))
                return Unauthorized();

            if ((!string.IsNullOrWhiteSpace(qas.Q1) && string.IsNullOrWhiteSpace(qas.A1)) ||
                 (!string.IsNullOrWhiteSpace(qas.Q2) && string.IsNullOrWhiteSpace(qas.A2)) ||
                 (!string.IsNullOrWhiteSpace(qas.Q3) && string.IsNullOrWhiteSpace(qas.A3)))
            {
                return BadRequest("有的问题没有填写答案");
            }
            // 更新密码
            UpdateSecurity(userId);
            TempData["SuccessMessage"] = "密保问题已修改";

            return new OkResult();
        }

        // 兼容原有的POST方法（如果前端还有普通表单提交）
        //public IActionResult OnPost()
        //{
        //    if (!int.TryParse(User.FindFirst("UserId")?.Value, out var userId))
        //        return Unauthorized();

        //    // 密码确认检查
        //    if (!string.IsNullOrWhiteSpace(NewPassword) && NewPassword != ConfirmPassword)
        //    {
        //        ModelState.AddModelError(nameof(NewPassword), "两次输入的密码不一致");
        //        return Page();
        //    }

        //    // 唯一性检查
        //    if (!ValidateUniqueness(userId)) return Page();

        //    // 更新数据库
        //    UpdateUserProfile(userId);
        //    TempData["SuccessMessage"] = "信息更新成功！";
        //    return RedirectToPage();
        //}

        private User GetUserProfile()
        {
            var userId = int.Parse(User.FindFirst("UserId")!.Value);
            using var conn = new OracleConnection(_conf.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = new OracleCommand(
                "SELECT user_name, email, user_type, status " +
                "FROM Users " +
                "WHERE user_id=:id",
                conn);
            cmd.Parameters.Add("id", userId);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new User
                {
                    UserName = r["user_name"].ToString()!,
                    Email = r["email"] == DBNull.Value ? null : r["email"].ToString(),
                    UserType = r["user_type"].ToString()!,
                    Status = r["status"].ToString()!
                };
            }
            else return new User();
        }

        private bool ValidateUniqueness(int userId)
        {
            if (DbUniqueChecker.Exists(_conf, "USER_NAME", UserInfo.UserName, excludeId: userId))
                return false;

            if (!string.IsNullOrWhiteSpace(UserInfo.Email) &&
                DbUniqueChecker.Exists(_conf, "EMAIL", UserInfo.Email, excludeId: userId))
                return false;

            return true;
        }

        private void UpdateUserProfile(int userId)
        {
            using var conn = new OracleConnection(_conf.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(
                "UPDATE Users " +
                "SET " +
                "user_name = :un, " +
                "email = :em, " +
                "WHERE user_id = :id",
                conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("un", UserInfo.UserName);
            cmd.Parameters.Add("em", string.IsNullOrWhiteSpace(UserInfo.Email) ? DBNull.Value : UserInfo.Email);
            cmd.Parameters.Add("id", userId);

            cmd.ExecuteNonQuery();
        }

        private void UpdatePassword(int userId)
        {
            using var conn = new OracleConnection(_conf.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(
                "UPDATE Users SET password = :pw WHERE user_id = :id", conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("pw", PasswordHelper.HashPassword(NewPassword!));
            cmd.Parameters.Add("id", userId);

            cmd.ExecuteNonQuery();
        }

        private QAs GetSecurityQAs()
        {
            var userId = User.FindFirst("UserId")?.Value;
            using var conn = new OracleConnection(_conf.GetConnectionString("OracleDb"));
            conn.Open();
            using var questionCmd = new OracleCommand(
                @"SELECT question_text, 
                  answer_text 
                  FROM user_answers
                  WHERE user_id = :id", conn);
            questionCmd.Parameters.Add("id", int.Parse(userId!));
            using var reader = questionCmd.ExecuteReader();
            if (reader.Read())
            {
                return new QAs()
                {
                    Q1 = reader["question_text"] == DBNull.Value ? "" : reader["question_text"].ToString(),
                    A1 = reader["answer_text"] == DBNull.Value ? "" : reader["answer_text"].ToString(),
                };
            }
            else return new QAs();
        }

        public void UpdateSecurity(int userId)
        {
            using var conn = new OracleConnection(_conf.GetConnectionString("OracleDb"));
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
                    question_text = :q1, answer_text = :a1,
                    WHERE user_id = :id",
                conn);
            }
            else
            {
                updateCmd = new OracleCommand(@"
				    INSERT INTO user_answers(
                        user_id, question_num,
                        question_text, answer_text,
                    )
                    VALUES (:id, :num, :q1, :a1)",
                conn);
            }
            updateCmd.BindByName = true;
            int questionCount = 0;
            if (!string.IsNullOrWhiteSpace(qas.Q1)) questionCount++;

            updateCmd.Parameters.Add("id", userId);
            updateCmd.Parameters.Add("num", questionCount);
            updateCmd.Parameters.Add("q1", string.IsNullOrWhiteSpace(qas.Q1) ? DBNull.Value : qas.Q1);
            updateCmd.Parameters.Add("a1", string.IsNullOrWhiteSpace(qas.A1) ? DBNull.Value : qas.A1);

            updateCmd.ExecuteNonQuery();

        }
    }
}
