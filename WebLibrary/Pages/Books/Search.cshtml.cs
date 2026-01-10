using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebLibrary.Pages.Shared.Models;
using System.IO; // Added for StreamReader
using System.Text.Json; // Added for JsonDocument
using System.Security.Claims;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Books
{
    public class SearchModel : PageModel
    {
        private readonly IConfiguration _cfg;

        public SearchModel(IConfiguration cfg) => _cfg = cfg;

        [BindProperty]
        public string? SearchKeyword { get; set; }

        [BindProperty]
        public string? SearchTitle { get; set; }

        [BindProperty]
        public string? SearchAuthor { get; set; }

        [BindProperty]
        public string? SearchCategory { get; set; }

        [BindProperty]
        public string? SearchISBN { get; set; }

        [BindProperty]
        public string? SearchPublisher { get; set; }

        [BindProperty]
        public DateTime? SearchPublicationDate { get; set; }

        [BindProperty]
        public bool OnlyShowAvailable { get; set; }

        public List<Book> SearchResults { get; set; } = new List<Book>();

        public struct Category
        {
            public string Name { get; set; }
            public int Id { get; set; }
        }
        public List<Category> BookCategories { get; set; } = new List<Category>();

        public async Task OnGetAsync(string? keyword = null, string? title = null, string? author = null,
            string? category = null, string? isbn = null, string? publisher = null, string? publicationDate = null,
            bool onlyShowAvailable = false)
        {
            // 获取所有图书分类
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            await conn.OpenAsync();

            using var cmd = new OracleCommand("SELECT CATEGORY_ID, CATEGORY_NAME FROM CATEGORY", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                BookCategories.Add(new Category
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }

            // 如果有搜索参数，执行搜索
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                SearchKeyword = keyword;
                OnlyShowAvailable = onlyShowAvailable;
                SearchResults = await SearchBooksAsync(keyword, onlyShowAvailable);
            }
            else if (!string.IsNullOrWhiteSpace(title) || !string.IsNullOrWhiteSpace(author) ||
                     !string.IsNullOrWhiteSpace(category) || !string.IsNullOrWhiteSpace(isbn) ||
                     !string.IsNullOrWhiteSpace(publisher) || !string.IsNullOrWhiteSpace(publicationDate))
            {
                // 设置高级搜索参数
                SearchTitle = title;
                SearchAuthor = author;
                SearchCategory = category;
                SearchISBN = isbn;
                SearchPublisher = publisher;
                OnlyShowAvailable = onlyShowAvailable;
                if (DateTime.TryParse(publicationDate, out var pubDate))
                {
                    SearchPublicationDate = pubDate;
                }

                SearchResults = await AdvancedSearchBooksAsync(title, author, category, isbn, publisher,
                    DateTime.TryParse(publicationDate, out var date) ? date : null, onlyShowAvailable);
            }
            else
            {
                // 页面初始加载时不显示任何结果
                SearchResults = new List<Book>();
            }
        }

        public IActionResult OnPostBasicSearch()
        {
            // 使用PRG模式：POST后重定向到GET
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                return RedirectToPage("Search", new { keyword = SearchKeyword });
            }
            else
            {
                return RedirectToPage("Search");
            }
        }

        public IActionResult OnPostAdvancedSearch()
        {
            // 使用PRG模式：POST后重定向到GET
            var parameters = new Dictionary<string, string?>();
            
            if (!string.IsNullOrWhiteSpace(SearchTitle))
                parameters.Add("title", SearchTitle);
            if (!string.IsNullOrWhiteSpace(SearchAuthor))
                parameters.Add("author", SearchAuthor);
            if (!string.IsNullOrWhiteSpace(SearchCategory))
                parameters.Add("category", SearchCategory);
            if (!string.IsNullOrWhiteSpace(SearchISBN))
                parameters.Add("isbn", SearchISBN);
            if (!string.IsNullOrWhiteSpace(SearchPublisher))
                parameters.Add("publisher", SearchPublisher);
            if (SearchPublicationDate.HasValue)
                parameters.Add("publicationDate", SearchPublicationDate.Value.ToString("yyyy-MM-dd"));
            if (OnlyShowAvailable)
                parameters.Add("onlyShowAvailable", "true");
            
            return RedirectToPage("Search", parameters);
        }

        public async Task<IActionResult> OnPostBorrowAsync(string bookId)
        {
            if (string.IsNullOrEmpty(bookId))
            {
                TempData["ErrorMessage"] = "无效的图书ID。";
                return RedirectWithCurrentSearchParameters();
            }

            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // 1. 获取当前用户ID
                var userId = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "用户未登录。";
                    return RedirectWithCurrentSearchParameters();
                }

                // 2. 获取用户角色和信用分
                string role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Student";
                string? creditStr = User.FindFirst("CreditScore")?.Value;
                int creditScore = string.IsNullOrEmpty(creditStr) ? 60 : int.Parse(creditStr);

                // 3. 检查借阅资格
                var (canBorrow, maxBooks, currentBorrows, message) = BorrowLimitChecker.CheckBorrowingEligibility(
                    int.Parse(userId), role, creditScore, _cfg);
                
                if (!canBorrow)
                {
                    TempData["ErrorMessage"] = message;
                    return RedirectWithCurrentSearchParameters();
                }

                // 3.5. 检查用户是否已经借阅了这本书
                using var cmdCheckExisting = new OracleCommand(@"
                    SELECT COUNT(*) 
                    FROM BorrowRecord br 
                    JOIN Copy c ON br.copy_id = c.copy_id 
                    WHERE c.book_id = :bookId 
                      AND br.user_id = :userId 
                      AND br.status IN ('lending', 'fined')", conn);
                cmdCheckExisting.Parameters.Add("bookId", bookId);
                cmdCheckExisting.Parameters.Add("userId", int.Parse(userId));
                var existingBorrows = Convert.ToInt32(await cmdCheckExisting.ExecuteScalarAsync());
                
                if (existingBorrows > 0)
                {
                    TempData["ErrorMessage"] = "您已经借阅了这本书，不能重复借阅同一本书。";
                    return RedirectWithCurrentSearchParameters();
                }

                // 4. 使用行锁查找并锁定可用副本（防止并发问题）
                using var cmdFindCopy = new OracleCommand(
                    "SELECT COPY_ID FROM COPY WHERE BOOK_ID = :bookId AND STATUS = 'AVAILABLE' AND ROWNUM = 1 FOR UPDATE", conn);
                cmdFindCopy.Parameters.Add("bookId", bookId);
                var copyIdObj = await cmdFindCopy.ExecuteScalarAsync();
                if (copyIdObj == null)
                {
                    TempData["ErrorMessage"] = "没有可用副本，请尝试预约。";
                    return RedirectWithCurrentSearchParameters();
                }
                var copyId = copyIdObj.ToString()!;

                // 5. 生成新借阅记录ID
                using var cmdMaxRecordId = new OracleCommand("SELECT NVL(MAX(RECORD_ID),0) FROM BORROWRECORD", conn);
                var maxRecordId = Convert.ToInt32(await cmdMaxRecordId.ExecuteScalarAsync());
                var newRecordId = maxRecordId + 1;

                // 6. 使用 BorrowRuleCalculator 计算借阅天数
                int finalDays = BorrowRuleCalculator.CalculateBorrowDays(role, creditScore, _cfg);

                // 7. 插入借阅记录
                var borrowDate = DateTime.Now;
                var dueDate = borrowDate.AddDays(finalDays);
                var status = "lending";
                var renewTimes = 0;
                using var cmdInsert = new OracleCommand(
                    "INSERT INTO BORROWRECORD (RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, STATUS, RENEW_TIMES) " +
                    "VALUES (:recordId, :userId, :copyId, :borrowDate, :dueDate, :status, :renewTimes)", conn);
                cmdInsert.Parameters.Add("recordId", newRecordId);
                cmdInsert.Parameters.Add("userId", int.Parse(userId));
                cmdInsert.Parameters.Add("copyId", int.Parse(copyId));
                cmdInsert.Parameters.Add("borrowDate", borrowDate);
                cmdInsert.Parameters.Add("dueDate", dueDate);
                cmdInsert.Parameters.Add("status", status);
                cmdInsert.Parameters.Add("renewTimes", renewTimes);
                await cmdInsert.ExecuteNonQueryAsync();

                // 8. 更新副本状态
                using var cmdUpdateCopy = new OracleCommand(
                    "UPDATE COPY SET STATUS = 'BORROWED' WHERE COPY_ID = :copyId", conn);
                cmdUpdateCopy.Parameters.Add("copyId", int.Parse(copyId));
                await cmdUpdateCopy.ExecuteNonQueryAsync();

                TempData["Message"] = $"借阅成功！{message}应在{finalDays}天内归还。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"借阅失败: {ex.Message}";
            }

            return RedirectWithCurrentSearchParameters();
        }

        // 预约功能
        public async Task<IActionResult> OnPostReserveAsync(string bookId)
        {
            if (string.IsNullOrEmpty(bookId))
            {
                TempData["ErrorMessage"] = "无效的图书ID。";
                return RedirectWithCurrentSearchParameters();
            }

            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // 获取当前用户ID
                var userId = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "用户未登录。";
                    return RedirectWithCurrentSearchParameters();
                }

                // 获取下一个reservation_id
                using var cmdMaxReservationId = new OracleCommand("SELECT NVL(MAX(reservation_id),0) FROM Reservation", conn);
                var maxReservationId = Convert.ToInt32(await cmdMaxReservationId.ExecuteScalarAsync());
                var newReservationId = maxReservationId + 1;

                // 获取当前该书的最大排队位次
                using var cmdMaxQueue = new OracleCommand("SELECT NVL(MAX(queue_position),0) FROM Reservation WHERE book_id = :bookId AND status IN ('pending','notified')", conn);
                cmdMaxQueue.Parameters.Add("bookId", bookId);
                var maxQueue = Convert.ToInt32(await cmdMaxQueue.ExecuteScalarAsync());
                var newQueuePosition = maxQueue + 1;

                // 插入预约记录
                var now = DateTime.Now.Date;
                var expiry = now.AddDays(30); // 预约有效期30天
                using var cmdInsert = new OracleCommand(
                    "INSERT INTO Reservation (reservation_id, user_id, book_id, reservation_date, status, expiry_date, queue_position) " +
                    "VALUES (:reservation_id, :user_id, :book_id, :reservation_date, :status, :expiry_date, :queue_position)", conn);
                cmdInsert.Parameters.Add("reservation_id", newReservationId);
                cmdInsert.Parameters.Add("user_id", int.Parse(userId));
                cmdInsert.Parameters.Add("book_id", int.Parse(bookId));
                cmdInsert.Parameters.Add("reservation_date", now);
                cmdInsert.Parameters.Add("status", "pending");
                cmdInsert.Parameters.Add("expiry_date", expiry);
                cmdInsert.Parameters.Add("queue_position", newQueuePosition);
                await cmdInsert.ExecuteNonQueryAsync();

                TempData["Message"] = "预约成功！您已加入预约队列。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"预约失败: {ex.Message}";
            }

            return RedirectWithCurrentSearchParameters();
        }

        private IActionResult RedirectWithCurrentSearchParameters()
        {
            // 保持当前搜索条件，重定向回搜索页面
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                return RedirectToPage("Search", new { keyword = SearchKeyword });
            }
            else if (!string.IsNullOrWhiteSpace(SearchTitle) || !string.IsNullOrWhiteSpace(SearchAuthor) || 
                     !string.IsNullOrWhiteSpace(SearchCategory) || !string.IsNullOrWhiteSpace(SearchISBN) || 
                     !string.IsNullOrWhiteSpace(SearchPublisher) || SearchPublicationDate != null)
            {
                var parameters = new Dictionary<string, string?>();
                
                if (!string.IsNullOrWhiteSpace(SearchTitle))
                    parameters.Add("title", SearchTitle);
                if (!string.IsNullOrWhiteSpace(SearchAuthor))
                    parameters.Add("author", SearchAuthor);
                if (!string.IsNullOrWhiteSpace(SearchCategory))
                    parameters.Add("category", SearchCategory);
                if (!string.IsNullOrWhiteSpace(SearchISBN))
                    parameters.Add("isbn", SearchISBN);
                if (!string.IsNullOrWhiteSpace(SearchPublisher))
                    parameters.Add("publisher", SearchPublisher);
                if (SearchPublicationDate.HasValue)
                    parameters.Add("publicationDate", SearchPublicationDate.Value.ToString("yyyy-MM-dd"));
                if (OnlyShowAvailable)
                    parameters.Add("onlyShowAvailable", "true");
                
                return RedirectToPage("Search", parameters);
            }
            else
            {
                return RedirectToPage("Search");
            }
        }

        private async Task<List<Book>> SearchBooksAsync(string keyword, bool onlyShowAvailable = false)
        {
            var books = new List<Book>();

            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // 更新SQL查询以匹配您的数据库架构
                var sql = @"
                    SELECT b.BOOK_ID, b.TITLE, b.AUTHOR, b.ISBN, b.CATEGORY_ID, 
                           b.PUBLISHER, b.PUBLICATION_DATE, b.DESCRIPTION, b.BOOK_RATING,
                           b.TOTAL_COPIES, b.AVAILABLE_COPIES,
                           c.CATEGORY_NAME
                    FROM BOOK b
                    LEFT JOIN CATEGORY c ON b.CATEGORY_ID = c.CATEGORY_ID
                    WHERE (UPPER(b.TITLE) LIKE :keyword 
                           OR UPPER(b.AUTHOR) LIKE :keyword 
                           OR UPPER(b.ISBN) LIKE :keyword
                           OR UPPER(c.CATEGORY_NAME) LIKE :keyword
                           OR UPPER(b.PUBLISHER) LIKE :keyword)";

                if (onlyShowAvailable)
                {
                    sql += " AND b.AVAILABLE_COPIES > 0";
                }

                sql += " ORDER BY b.TITLE";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add("keyword", $"%{keyword.ToUpper()}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    books.Add(MapReaderToBook(reader));
                }
            }
            catch (Exception ex)
            {
                // 记录错误（实际项目中应该使用日志框架）
                Console.WriteLine($"搜索书籍时发生错误: {ex.Message}");
                
                // 如果CATEGORY表不存在，使用简化查询
                if (ex.Message.Contains("invalid identifier") || ex.Message.Contains("table or view does not exist"))
                {
                    books = await SearchBooksSimpleAsync(keyword, onlyShowAvailable);
                }
            }

            return books;
        }

        private async Task<List<Book>> SearchBooksSimpleAsync(string keyword, bool onlyShowAvailable = false)
        {
            var books = new List<Book>();

            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // 简化的SQL查询，不包含分类表连接
                var sql = @"
                    SELECT BOOK_ID, TITLE, AUTHOR, ISBN, CATEGORY_ID, 
                           PUBLISHER, PUBLICATION_DATE, DESCRIPTION, BOOK_RATING,
                           TOTAL_COPIES, AVAILABLE_COPIES
                    FROM BOOK
                    WHERE (UPPER(TITLE) LIKE :keyword 
                           OR UPPER(AUTHOR) LIKE :keyword 
                           OR UPPER(ISBN) LIKE :keyword)";

                if (onlyShowAvailable)
                {
                    sql += " AND AVAILABLE_COPIES > 0";
                }

                sql += " ORDER BY TITLE";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add("keyword", $"%{keyword.ToUpper()}%");

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    books.Add(MapReaderToBookSimple(reader));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"简化搜索书籍时发生错误: {ex.Message}");
            }

            return books;
        }

        private async Task<List<Book>> AdvancedSearchBooksAsync(string? title, string? author, string? category, string? isbn, string? publisher, DateTime? publicationDate, bool onlyShowAvailable = false)
        {
            var books = new List<Book>();

            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                var conditions = new List<string>();
                var parameters = new List<OracleParameter>();

                // 构建动态WHERE条件
                if (!string.IsNullOrWhiteSpace(title))
                {
                    conditions.Add("UPPER(b.TITLE) LIKE :title");
                    parameters.Add(new OracleParameter("title", $"%{title.ToUpper()}%"));
                }

                if (!string.IsNullOrWhiteSpace(author))
                {
                    conditions.Add("UPPER(b.AUTHOR) LIKE :author");
                    parameters.Add(new OracleParameter("author", $"%{author.ToUpper()}%"));
                }

                if (!string.IsNullOrWhiteSpace(category))
                {
                    conditions.Add("UPPER(c.CATEGORY_NAME) = :category");
                    parameters.Add(new OracleParameter("category", category.ToUpper()));
                }

                if (!string.IsNullOrWhiteSpace(SearchISBN))
                {
                    conditions.Add("UPPER(ISBN) LIKE :isbn");
                    parameters.Add(new OracleParameter("isbn", $"%{SearchISBN.ToUpper()}%"));
                }

                if (!string.IsNullOrWhiteSpace(SearchPublisher))
                {
                    conditions.Add("UPPER(PUBLISHER) LIKE :publisher");
                    parameters.Add(new OracleParameter("publisher", $"%{SearchPublisher.ToUpper()}%"));
                }

                if (DateTime.TryParse(SearchPublicationDate?.ToString(), out var pubDate))
                {
                    conditions.Add("PUBLICATION_DATE >= :publicationDate");
                    parameters.Add(new OracleParameter("publicationDate", pubDate));
                }

                // 基础查询
                var sql = @"
                    SELECT b.BOOK_ID, b.TITLE, b.AUTHOR, b.ISBN, b.CATEGORY_ID, 
                           b.PUBLISHER, b.PUBLICATION_DATE, b.DESCRIPTION, b.BOOK_RATING,
                           b.TOTAL_COPIES, b.AVAILABLE_COPIES,
                           c.CATEGORY_NAME
                    FROM BOOK b
                    LEFT JOIN CATEGORY c ON b.CATEGORY_ID = c.CATEGORY_ID";

                // 添加搜索条件
                if (conditions.Count > 0)
                {
                    sql += " WHERE " + string.Join(" AND ", conditions);
                }

                // 添加只看可借的条件
                if (onlyShowAvailable)
                {
                    if (conditions.Count > 0)
                    {
                        sql += " AND b.AVAILABLE_COPIES > 0";
                    }
                    else
                    {
                        sql += " WHERE b.AVAILABLE_COPIES > 0";
                    }
                }

                sql += " ORDER BY b.TITLE";

                using var cmd = new OracleCommand(sql, conn);
                foreach (var param in parameters)
                {
                    cmd.Parameters.Add(param);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    books.Add(MapReaderToBook(reader));
                }
            }
            catch (Exception ex)
            {
                // 记录错误（实际项目中应该使用日志框架）
                Console.WriteLine($"高级搜索书籍时发生错误: {ex.Message}");
                
                // 如果CATEGORY表不存在，使用简化查询
                if (ex.Message.Contains("invalid identifier") || ex.Message.Contains("table or view does not exist"))
                {
                    books = await AdvancedSearchBooksSimpleAsync(title, author, category, isbn, publisher, publicationDate, onlyShowAvailable);
                }
            }

            return books;
        }

        private async Task<List<Book>> AdvancedSearchBooksSimpleAsync(string? title, string? author, string? category, string? isbn, string? publisher, DateTime? publicationDate, bool onlyShowAvailable = false)
        {
            var books = new List<Book>();

            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                var conditions = new List<string>();
                var parameters = new List<OracleParameter>();

                // 构建动态WHERE条件（简化版本）
                if (!string.IsNullOrWhiteSpace(title))
                {
                    conditions.Add("UPPER(TITLE) LIKE :title");
                    parameters.Add(new OracleParameter("title", $"%{title.ToUpper()}%"));
                }

                if (!string.IsNullOrWhiteSpace(author))
                {
                    conditions.Add("UPPER(AUTHOR) LIKE :author");
                    parameters.Add(new OracleParameter("author", $"%{author.ToUpper()}%"));
                }

                if (!string.IsNullOrWhiteSpace(category))
                {
                    conditions.Add("UPPER(CATEGORY_ID) = :category");
                    parameters.Add(new OracleParameter("category", category.ToUpper()));
                }

                if (!string.IsNullOrWhiteSpace(SearchISBN))
                {
                    conditions.Add("UPPER(ISBN) LIKE :isbn");
                    parameters.Add(new OracleParameter("isbn", $"%{SearchISBN.ToUpper()}%"));
                }

                if (!string.IsNullOrWhiteSpace(SearchPublisher))
                {
                    conditions.Add("UPPER(PUBLISHER) LIKE :publisher");
                    parameters.Add(new OracleParameter("publisher", $"%{SearchPublisher.ToUpper()}%"));
                }

                if (DateTime.TryParse(SearchPublicationDate?.ToString(), out var pubDate))
                {
                    conditions.Add("PUBLICATION_DATE >= :publicationDate");
                    parameters.Add(new OracleParameter("publicationDate", pubDate));
                }

                // 基础查询
                var sql = @"
                    SELECT BOOK_ID, TITLE, AUTHOR, ISBN, CATEGORY_ID, 
                           PUBLISHER, PUBLICATION_DATE, DESCRIPTION, BOOK_RATING,
                           TOTAL_COPIES, AVAILABLE_COPIES
                    FROM BOOK";

                // 添加搜索条件
                if (conditions.Count > 0)
                {
                    sql += " WHERE " + string.Join(" AND ", conditions);
                }

                // 添加只看可借的条件
                if (onlyShowAvailable)
                {
                    if (conditions.Count > 0)
                    {
                        sql += " AND AVAILABLE_COPIES > 0";
                    }
                    else
                    {
                        sql += " WHERE AVAILABLE_COPIES > 0";
                    }
                }

                sql += " ORDER BY TITLE";

                using var cmd = new OracleCommand(sql, conn);
                foreach (var param in parameters)
                {
                    cmd.Parameters.Add(param);
                }

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    books.Add(MapReaderToBookSimple(reader));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"简化高级搜索书籍时发生错误: {ex.Message}");
            }

            return books;
        }

        private Book MapReaderToBook(OracleDataReader reader)
        {
            return new Book
            {
                BookId = reader["BOOK_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["BOOK_ID"]),
                Title = reader["TITLE"]?.ToString(),
                Author = reader["AUTHOR"]?.ToString(),
                ISBN = reader["ISBN"]?.ToString(),
                CategoryId = reader["CATEGORY_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["CATEGORY_ID"]),
                CategoryName = reader["CATEGORY_NAME"]?.ToString(),
                Publisher = reader["PUBLISHER"]?.ToString(),
                PublicationDate = reader["PUBLICATION_DATE"] == DBNull.Value ? null : Convert.ToDateTime(reader["PUBLICATION_DATE"]),
                Description = reader["DESCRIPTION"]?.ToString(),
                BookRating = reader["BOOK_RATING"] == DBNull.Value ? null : Convert.ToDecimal(reader["BOOK_RATING"]),
                TotalCopies = reader["TOTAL_COPIES"] == DBNull.Value ? null : Convert.ToInt32(reader["TOTAL_COPIES"]),
                AvailableCopies = reader["AVAILABLE_COPIES"] == DBNull.Value ? null : Convert.ToInt32(reader["AVAILABLE_COPIES"])
            };
        }

        private Book MapReaderToBookSimple(OracleDataReader reader)
        {
            return new Book
            {
                BookId = reader["BOOK_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["BOOK_ID"]),
                Title = reader["TITLE"]?.ToString(),
                Author = reader["AUTHOR"]?.ToString(),
                ISBN = reader["ISBN"]?.ToString(),
                CategoryId = reader["CATEGORY_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["CATEGORY_ID"]),
                CategoryName = reader["CATEGORY_ID"]?.ToString(), // 使用CATEGORY_ID作为名称（如果没有分类表）
                Publisher = reader["PUBLISHER"]?.ToString(),
                PublicationDate = reader["PUBLICATION_DATE"] == DBNull.Value ? null : Convert.ToDateTime(reader["PUBLICATION_DATE"]),
                Description = reader["DESCRIPTION"]?.ToString(),
                BookRating = reader["BOOK_RATING"] == DBNull.Value ? null : Convert.ToDecimal(reader["BOOK_RATING"]),
                TotalCopies = reader["TOTAL_COPIES"] == DBNull.Value ? null : Convert.ToInt32(reader["TOTAL_COPIES"]),
                AvailableCopies = reader["AVAILABLE_COPIES"] == DBNull.Value ? null : Convert.ToInt32(reader["AVAILABLE_COPIES"])
            };
        }
    }
}