using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel.DataAnnotations;

namespace WebLibrary.Pages.Review
{
    [Authorize]
    public class CreateReviewModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<CreateReviewModel> _logger;

        public CreateReviewModel(IConfiguration config, ILogger<CreateReviewModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public int? BookId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BookTitle { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BookAuthor { get; set; }

        [BindProperty]
        public ReviewInput Input { get; set; } = new();

        public class ReviewInput
        {
            [Required(ErrorMessage = "请选择图书")]
            public int BookId { get; set; }

            [Required(ErrorMessage = "请输入图书名称")]
            public string BookTitle { get; set; } = "";

            [Required(ErrorMessage = "请选择评分")]
            [Range(1, 5, ErrorMessage = "评分必须在1-5之间")]
            public int Rating { get; set; } = 3;

            [Required(ErrorMessage = "评论内容不能为空")]
            [StringLength(500, ErrorMessage = "评论内容不能超过500个字符")]
            public string Content { get; set; } = "";
        }

        public IActionResult OnGet()
        {
            // 如果有传递过来的书籍信息，则自动填充
            if (BookId.HasValue && BookId > 0 && !string.IsNullOrEmpty(BookTitle))
            {
                Input.BookId = BookId.Value;
                Input.BookTitle = BookTitle;
            }

            return Page();
        }

        public JsonResult OnGetBookSearch(string query)
        {
            var books = new List<BookSearchResult>();

            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                conn.Open();

                using var cmd = new OracleCommand(@"
                    SELECT book_id, title, author 
                    FROM book 
                    WHERE UPPER(title) LIKE UPPER(:query) || '%'
                    ORDER BY title
                    FETCH FIRST 10 ROWS ONLY", conn);

                cmd.Parameters.Add("query", OracleDbType.Varchar2).Value = query;

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    books.Add(new BookSearchResult
                    {
                        BookId = Convert.ToInt32(reader["book_id"]),
                        Title = reader["title"].ToString()!,
                        Author = reader["author"] as string
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索图书时出错");
            }

            return new JsonResult(books);
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid) return Page();

            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return RedirectToPage("/Account/Login");

            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                conn.Open();

                using var transaction = conn.BeginTransaction();
                try
                {
                    // 验证图书ID是否存在
                    using var verifyCmd = new OracleCommand(
                        "SELECT COUNT(*) FROM book WHERE book_id = :book_id", conn);
                    verifyCmd.Parameters.Add("book_id", OracleDbType.Int32).Value = Input.BookId;

                    if (Convert.ToInt32(verifyCmd.ExecuteScalar()) == 0)
                    {
                        ModelState.AddModelError("Input.BookTitle", "选择的图书不存在");
                        return Page();
                    }

                    // 插入新评论
                    using var insertCmd = new OracleCommand(
                        "INSERT INTO Comments (comment_id, user_id, book_id, content, comment_date, rating) " +
                        "VALUES (COMMENTS_SEQ.NEXTVAL, :user_id, :book_id, :content, SYSDATE, :rating)", conn);

                    insertCmd.Parameters.Add("user_id", OracleDbType.Int32).Value = int.Parse(userId);
                    insertCmd.Parameters.Add("book_id", OracleDbType.Int32).Value = Input.BookId;
                    insertCmd.Parameters.Add("content", OracleDbType.NClob).Value = Input.Content;
                    insertCmd.Parameters.Add("rating", OracleDbType.Int32).Value = Input.Rating;

                    if (insertCmd.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "提交评论失败，请重试");
                        return Page();
                    }

                    // 计算并更新图书平均评分
                    using var avgCmd = new OracleCommand(
                    "SELECT ROUND(AVG(rating), 1) FROM Comments WHERE book_id = :book_id", conn);
                    avgCmd.Parameters.Add("book_id", OracleDbType.Int32).Value = Input.BookId;

                    var avgResult = avgCmd.ExecuteScalar();
                    decimal newRating = avgResult != DBNull.Value ? Convert.ToDecimal(avgResult) : 0;

                    using var updateCmd = new OracleCommand(
                        "UPDATE book SET book_rating = :new_rating WHERE book_id = :book_id", conn);
                    updateCmd.Parameters.Add("new_rating", OracleDbType.Decimal).Value = newRating;
                    updateCmd.Parameters.Add("book_id", OracleDbType.Int32).Value = Input.BookId;

                    if (updateCmd.ExecuteNonQuery() != 1)
                    {
                        transaction.Rollback();
                        ModelState.AddModelError("", "提交评论失败，请重试");
                        return Page();
                    }

                    transaction.Commit();
                    return RedirectToPage("/Review/Index");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "提交评论时发生数据库错误");
                    ModelState.AddModelError("", "提交评论时出错: " + ex.Message);
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Oracle数据库错误");
                ModelState.AddModelError("", $"数据库错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "提交评论时发生未知错误");
                ModelState.AddModelError("", "提交评论时出错: " + ex.Message);
            }

            return Page();
        }

        private class BookSearchResult
        {
            public int BookId { get; set; }
            public string Title { get; set; } = "";
            public string? Author { get; set; }
        }
    }
}