using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;

namespace WebLibrary.Pages.Review
{
    [Authorize]
    public class MyReviewsModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MyReviewsModel> _logger;

        public MyReviewsModel(IConfiguration config, ILogger<MyReviewsModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        public List<Comment> Comments { get; set; } = new();

        public async Task OnGetAsync()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return;

            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                using var cmd = new OracleCommand(@"
                    SELECT c.comment_id, c.content, c.rating, c.comment_date, 
                           b.book_id, b.title, 
                           u.user_id, u.user_name
                    FROM comments c
                    JOIN book b ON c.book_id = b.book_id
                    JOIN users u ON c.user_id = u.user_id
                    WHERE c.user_id = :user_id
                    ORDER BY c.comment_date DESC", conn);

                cmd.Parameters.Add("user_id", OracleDbType.Int32).Value = int.Parse(userId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    Comments.Add(new Comment
                    {
                        CommentId = Convert.ToInt32(reader["comment_id"]),
                        Content = reader["content"] == DBNull.Value ? "" : reader["content"].ToString(),
                        Rating = Convert.ToInt32(reader["rating"]),
                        CommentDate = Convert.ToDateTime(reader["comment_date"]),
                        BookId = Convert.ToInt32(reader["book_id"]),
                        BookTitle = reader["title"] == DBNull.Value ? "无标题" : reader["title"].ToString(),
                        UserId = Convert.ToInt32(reader["user_id"]),
                        Username = reader["user_name"] == DBNull.Value ? "匿名用户" : reader["user_name"].ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取用户评论时出错");
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                return RedirectToPage("/Account/Login");

            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                using var transaction = await conn.BeginTransactionAsync();
                try
                {
                    // 获取评论关联的图书ID
                    int bookId;
                    using (var getBookCmd = new OracleCommand(
                        "SELECT book_id FROM comments WHERE comment_id = :comment_id AND user_id = :user_id", conn))
                    {
                        getBookCmd.Parameters.Add("comment_id", OracleDbType.Int32).Value = id;
                        getBookCmd.Parameters.Add("user_id", OracleDbType.Int32).Value = userId;
                        var result = await getBookCmd.ExecuteScalarAsync();
                        bookId = result != DBNull.Value ? Convert.ToInt32(result) : 0;
                    }

                    // 删除评论
                    using var deleteCmd = new OracleCommand(
                        "DELETE FROM comments WHERE comment_id = :comment_id AND user_id = :user_id", conn);
                    deleteCmd.Parameters.Add("comment_id", OracleDbType.Int32).Value = id;
                    deleteCmd.Parameters.Add("user_id", OracleDbType.Int32).Value = userId;
                    await deleteCmd.ExecuteNonQueryAsync();

                    // 更新图书评分
                    using var updateCmd = new OracleCommand(@"
                    UPDATE book SET book_rating = 
                    CASE 
                    WHEN (SELECT COUNT(*) FROM comments WHERE book_id = :book_id) = 0 THEN NULL
                    ELSE GREATEST(1, LEAST(5, 
                    ROUND((SELECT AVG(rating) FROM comments WHERE book_id = :book_id), 1)
                    ))
                    END
                    WHERE book_id = :book_id", conn);
                    updateCmd.Parameters.Add("book_id", OracleDbType.Int32).Value = bookId;
                    await updateCmd.ExecuteNonQueryAsync();

                    await transaction.CommitAsync();
                    return RedirectToPage();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "删除评论时出错");
                    ModelState.AddModelError("", "删除评论时出错: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除评论时发生错误");
                ModelState.AddModelError("", "删除评论时出错: " + ex.Message);
            }

            return Page();
        }

        public class Comment
        {
            public int CommentId { get; set; }
            public string Content { get; set; } = "";
            public int Rating { get; set; }
            public DateTime CommentDate { get; set; }
            public int BookId { get; set; }
            public string BookTitle { get; set; } = "";
            public int UserId { get; set; }
            public string Username { get; set; } = "";
        }
    }
}