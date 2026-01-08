using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel.DataAnnotations;

namespace WebLibrary.Pages.Review
{
    [Authorize]
    public class EditReviewModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EditReviewModel> _logger;

        public EditReviewModel(IConfiguration config, ILogger<EditReviewModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        [BindProperty]
        public ReviewInput Input { get; set; } = new();

        public string BookTitle { get; set; } = "";
        public string Author { get; set; } = "";

        public class ReviewInput
        {
            public int CommentId { get; set; }
            public int BookId { get; set; }

            [Required(ErrorMessage = "请选择评分")]
            [Range(1, 5, ErrorMessage = "评分必须在1-5之间")]
            public int Rating { get; set; } = 3;

            [Required(ErrorMessage = "评论内容不能为空")]
            [StringLength(500, ErrorMessage = "评论内容不能超过500个字符")]
            public string Content { get; set; } = "";
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return RedirectToPage("/Account/Login");

            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                using var cmd = new OracleCommand(@"
                    SELECT c.comment_id, c.content, c.rating, c.book_id, b.title, b.author
                    FROM comments c
                    JOIN book b ON c.book_id = b.book_id
                    WHERE c.comment_id = :comment_id AND c.user_id = :user_id", conn);

                cmd.Parameters.Add("comment_id", OracleDbType.Int32).Value = id;
                cmd.Parameters.Add("user_id", OracleDbType.Int32).Value = int.Parse(userId);

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    Input = new ReviewInput
                    {
                        CommentId = Convert.ToInt32(reader["comment_id"]),
                        Content = reader["content"] == DBNull.Value ? "" : reader["content"].ToString()!,
                        Rating = Convert.ToInt32(reader["rating"]),
                        BookId = Convert.ToInt32(reader["book_id"])
                    };
                    BookTitle = reader["title"] == DBNull.Value ? "无标题" : reader["title"].ToString()!;
                    Author = reader["author"] == DBNull.Value ? "未知作者" : reader["author"].ToString()!;
                }
                else
                {
                    return NotFound();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取评论编辑信息时出错");
                return StatusCode(500);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadBookInfo();
                return Page();
            }

            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return RedirectToPage("/Account/Login");

            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                using var transaction = await conn.BeginTransactionAsync();
                try
                {
                    // 更新评论
                    using var updateCmd = new OracleCommand(@"
                        UPDATE comments 
                        SET content = :content, rating = :rating, comment_date = SYSDATE
                        WHERE comment_id = :comment_id AND user_id = :user_id", conn);

                    updateCmd.Parameters.Add("content", OracleDbType.NClob).Value = Input.Content;
                    updateCmd.Parameters.Add("rating", OracleDbType.Int32).Value = Input.Rating;
                    updateCmd.Parameters.Add("comment_id", OracleDbType.Int32).Value = Input.CommentId;
                    updateCmd.Parameters.Add("user_id", OracleDbType.Int32).Value = int.Parse(userId);

                    if (await updateCmd.ExecuteNonQueryAsync() != 1)
                    {
                        await transaction.RollbackAsync();
                        ModelState.AddModelError("", "更新评论失败");
                        await LoadBookInfo();
                        return Page();
                    }

                    // 更新图书评分
                    using var updateRatingCmd = new OracleCommand(@"
                    UPDATE book SET book_rating = 
                    (SELECT ROUND(AVG(rating), 1) FROM comments WHERE book_id = :book_id)
                    WHERE book_id = :book_id", conn);
                    updateRatingCmd.Parameters.Add("book_id", OracleDbType.Int32).Value = Input.BookId;

                    await updateRatingCmd.ExecuteNonQueryAsync();
                    await transaction.CommitAsync();

                    return RedirectToPage("/Review/MyReviews");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "更新评论时出错");
                    ModelState.AddModelError("", "更新评论时出错: " + ex.Message);
                    await LoadBookInfo();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新评论时发生错误");
                ModelState.AddModelError("", "更新评论时出错: " + ex.Message);
                await LoadBookInfo();
            }

            return Page();
        }

        private async Task LoadBookInfo()
        {
            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                using var cmd = new OracleCommand(
                    "SELECT title, author FROM book WHERE book_id = :book_id", conn);
                cmd.Parameters.Add("book_id", OracleDbType.Int32).Value = Input.BookId;

                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    BookTitle = reader["title"] == DBNull.Value ? "无标题" : reader["title"].ToString()!;
                    Author = reader["author"] == DBNull.Value ? "未知作者" : reader["author"].ToString()!;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载图书信息时出错");
                BookTitle = "加载失败";
                Author = "加载失败";
            }
        }
    }
}