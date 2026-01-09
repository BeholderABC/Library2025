using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace WebLibrary.Pages.List
{
    public class HotBookListModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<HotBookListModel> _logger;

        public HotBookListModel(IConfiguration config, ILogger<HotBookListModel> logger)
        {
            _config = config;
            _logger = logger;
            CategoryHotBooks = new Dictionary<string, List<BookInfo>>();
        }

        public List<BookInfo> HotBooks { get; set; } = new List<BookInfo>();
        public Dictionary<string, List<BookInfo>> CategoryHotBooks { get; set; }

        public class BookInfo
        {
            public int BookId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Author { get; set; }
            public string? ISBN { get; set; }
            public string? Publisher { get; set; }
            public DateTime? PublicationDate { get; set; }
            public string? Description { get; set; }
            public int CategoryId { get; set; }
            public string? CategoryName { get; set; }

            [DisplayFormat(DataFormatString = "{0:0.0}")]
            public decimal BookRating { get; set; }

            [Display(Name = "借阅次数")]
            public int BorrowCount { get; set; }
        }

        public void OnGet()
        {
            try
            {
                using (var conn = new OracleConnection(_config.GetConnectionString("OracleDb")))
                {
                    conn.Open();

                    // 获取全站热门图书
                    HotBooks = GetHotBooks(conn);
                    _logger.LogInformation("获取全站热门图书: {Count} 本", HotBooks.Count);

                    // 获取分类热门图书
                    CategoryHotBooks = GetCategoryHotBooks(conn);
                    _logger.LogInformation("获取分类热门图书: {CategoryCount} 个分类", CategoryHotBooks.Count);
                }
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Oracle数据库错误，错误代码: {ErrorCode}", ex.Number);
                ModelState.AddModelError("", $"数据库错误(ORA-{ex.Number}): {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取热门图书列表时发生错误");
                ModelState.AddModelError("", "获取热门图书列表时出错: " + ex.Message);
            }
        }

        private List<BookInfo> GetHotBooks(OracleConnection conn)
        {
            var books = new List<BookInfo>();
            
            using (var cmd = new OracleCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    SELECT b.book_id, b.title, b.author, b.ISBN, b.publisher, 
                           b.publication_date, b.description, b.book_rating, 
                           b.borrow_count, b.category_id, c.category_name
                    FROM book b
                    JOIN category c ON b.category_id = c.category_id
                    WHERE b.book_rating >= 3
                    ORDER BY b.borrow_count DESC
                    FETCH FIRST 30 ROWS ONLY";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        books.Add(CreateBookInfoFromReader(reader));
                    }
                }
            }
            
            return books;
        }

        private Dictionary<string, List<BookInfo>> GetCategoryHotBooks(OracleConnection conn)
        {
            var categoryBooks = new Dictionary<string, List<BookInfo>>();
            
            using (var cmd = new OracleCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = @"
                    WITH ranked_books AS (
                        SELECT 
                            b.book_id, b.title, b.author, b.ISBN, b.publisher, 
                            b.publication_date, b.description, b.book_rating, 
                            b.borrow_count, b.category_id, c.category_name,
                            ROW_NUMBER() OVER (PARTITION BY b.category_id ORDER BY b.borrow_count DESC) as rank
                        FROM book b
                        JOIN category c ON b.category_id = c.category_id
                        WHERE b.book_rating >= 3
                    )
                    SELECT * FROM ranked_books WHERE rank <= 6
                    ORDER BY category_name, rank";

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var book = CreateBookInfoFromReader(reader);
                        var categoryName = book.CategoryName ?? "未知分类";
                        
                        if (!categoryBooks.ContainsKey(categoryName))
                        {
                            categoryBooks[categoryName] = new List<BookInfo>();
                        }
                        categoryBooks[categoryName].Add(book);
                    }
                }
            }
            
            return categoryBooks;
        }

        private BookInfo CreateBookInfoFromReader(OracleDataReader reader)
        {
            return new BookInfo
            {
                BookId = reader.GetSafeInt32("book_id"),
                Title = reader.GetSafeString("title"),
                Author = reader.GetSafeString("author"),
                ISBN = reader.GetSafeString("ISBN"),
                Publisher = reader.GetSafeString("publisher"),
                PublicationDate = reader.GetSafeDateTime("publication_date"),
                Description = reader.GetSafeString("description"),
                BookRating = reader.GetSafeDecimal("book_rating"),
                BorrowCount = reader.GetSafeInt32("borrow_count"),
                CategoryId = reader.GetSafeInt32("category_id"),
                CategoryName = reader.GetSafeString("category_name")
            };
        }
    }

    // 扩展方法用于安全地读取数据库值
    public static class OracleDataReaderExtensions
    {
        public static int GetSafeInt32(this OracleDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetInt32(ordinal);
        }

        public static string GetSafeString(this OracleDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
        }

        public static decimal GetSafeDecimal(this OracleDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? 0 : reader.GetDecimal(ordinal);
        }

        public static DateTime? GetSafeDateTime(this OracleDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
    }
}