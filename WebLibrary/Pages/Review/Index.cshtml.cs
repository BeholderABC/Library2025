using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;

namespace WebLibrary.Pages.Review
{
    public class IndexModel : PageModel
    {
        private readonly IConfiguration _config;
        
        public List<Comment> Comments { get; set; } = new();

        public IndexModel(IConfiguration config) => _config = config;

        public void OnGet()
        {
            try
            {
                using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
                conn.Open();
                
                using var cmd = new OracleCommand(@"
                    SELECT c.comment_id, c.content, c.rating, c.comment_date, 
                           b.book_id, b.title, 
                           u.user_id, u.user_name
                    FROM comments c
                    JOIN book b ON c.book_id = b.book_id
                    JOIN users u ON c.user_id = u.user_id
                    ORDER BY c.comment_date DESC
                    FETCH FIRST 10 ROWS ONLY", conn);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
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
                Console.WriteLine($"数据库查询错误: {ex.Message}");
            }
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