using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;
using WebLibrary.Pages.Shared.Models;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Books
{
    public class BookDetailModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public BookDetailModel(IConfiguration cfg) => _cfg = cfg;

        [BindProperty(SupportsGet = true)]
        public int? Id { get; set; }

        public Book? Book { get; set; }
        public List<Comment> Comments { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            Id = id;
            if (id == null)
            {
                TempData["ErrorMessage"] = "缺少图书ID。";
                return Page();
            }
            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();
                using var cmd = new OracleCommand(@"SELECT b.BOOK_ID, b.TITLE, b.AUTHOR, b.ISBN, b.CATEGORY_ID, b.PUBLISHER, b.PUBLICATION_DATE, b.DESCRIPTION, b.BOOK_RATING, b.TOTAL_COPIES, b.AVAILABLE_COPIES FROM BOOK b WHERE b.BOOK_ID = :id", conn);
                cmd.Parameters.Add("id", id);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    Book = new Book
                    {
                        BookId = reader["BOOK_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["BOOK_ID"]),
                        Title = reader["TITLE"]?.ToString(),
                        Author = reader["AUTHOR"]?.ToString(),
                        ISBN = reader["ISBN"]?.ToString(),
                        CategoryId = reader["CATEGORY_ID"] == DBNull.Value ? null : Convert.ToInt32(reader["CATEGORY_ID"]),
                        Publisher = reader["PUBLISHER"]?.ToString(),
                        PublicationDate = reader["PUBLICATION_DATE"] == DBNull.Value ? null : Convert.ToDateTime(reader["PUBLICATION_DATE"]),
                        Description = reader["DESCRIPTION"]?.ToString(),
                        BookRating = reader["BOOK_RATING"] == DBNull.Value ? null : Convert.ToDecimal(reader["BOOK_RATING"]),
                        TotalCopies = reader["TOTAL_COPIES"] == DBNull.Value ? null : Convert.ToInt32(reader["TOTAL_COPIES"]),
                        AvailableCopies = reader["AVAILABLE_COPIES"] == DBNull.Value ? null : Convert.ToInt32(reader["AVAILABLE_COPIES"])
                    };
                }
                else
                {
                    TempData["ErrorMessage"] = "未找到该图书。";
                }

                // 获取该图书的评论
                if (Book != null)
                {
                    using var commentCmd = new OracleCommand(@"
                        SELECT c.comment_id, c.content, c.rating, c.comment_date, 
                               u.user_id, u.user_name
                        FROM comments c
                        JOIN users u ON c.user_id = u.user_id
                        WHERE c.book_id = :book_id
                        ORDER BY c.comment_date DESC", conn);
                    
                    commentCmd.Parameters.Add("book_id", OracleDbType.Int32).Value = id;
                    
                    using var commentReader = await commentCmd.ExecuteReaderAsync();
                    while (await commentReader.ReadAsync())
                    {
                        Comments.Add(new Comment
                        {
                            CommentId = Convert.ToInt32(commentReader["comment_id"]),
                            Content = commentReader["content"] == DBNull.Value ? "" : commentReader["content"].ToString(),
                            Rating = Convert.ToInt32(commentReader["rating"]),
                            CommentDate = Convert.ToDateTime(commentReader["comment_date"]),
                            UserId = Convert.ToInt32(commentReader["user_id"]),
                            Username = commentReader["user_name"] == DBNull.Value ? "匿名用户" : commentReader["user_name"].ToString()
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"加载图书信息失败: {ex.Message}";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostBorrowAsync(int? bookId)
        {
            if (bookId == null)
            {
                TempData["ErrorMessage"] = "无效的图书ID。";
                return RedirectToPage(new { id = bookId });
            }
            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                var userId = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "用户未登录。";
                    return RedirectToPage(new { id = bookId });
                }

                string role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Student";
                string? creditStr = User.FindFirst("CreditScore")?.Value;
                int creditScore = string.IsNullOrEmpty(creditStr) ? 60 : int.Parse(creditStr);

                var (canBorrow, maxBooks, currentBorrows, message) = BorrowLimitChecker.CheckBorrowingEligibility(int.Parse(userId), role, creditScore, _cfg);
                if (!canBorrow)
                {
                    TempData["ErrorMessage"] = message;
                    return RedirectToPage(new { id = bookId });
                }

                using var cmdCheckExisting = new OracleCommand(@"SELECT COUNT(*) FROM BorrowRecord br JOIN Copy c ON br.copy_id = c.copy_id WHERE c.book_id = :bookId AND br.user_id = :userId AND br.status IN ('lending','fined')", conn);
                cmdCheckExisting.Parameters.Add("bookId", bookId);
                cmdCheckExisting.Parameters.Add("userId", int.Parse(userId));
                var existing = Convert.ToInt32(await cmdCheckExisting.ExecuteScalarAsync());
                if (existing > 0)
                {
                    TempData["ErrorMessage"] = "您已经借阅了这本书。";
                    return RedirectToPage(new { id = bookId });
                }

                using var cmdFindCopy = new OracleCommand("SELECT COPY_ID FROM COPY WHERE BOOK_ID = :bookId AND STATUS = 'AVAILABLE' AND ROWNUM = 1 FOR UPDATE", conn);
                cmdFindCopy.Parameters.Add("bookId", bookId);
                var copyIdObj = await cmdFindCopy.ExecuteScalarAsync();
                if (copyIdObj == null)
                {
                    TempData["ErrorMessage"] = "没有可用副本，请尝试预约。";
                    return RedirectToPage(new { id = bookId });
                }
                var copyId = Convert.ToInt32(copyIdObj);

                using var cmdMaxRecordId = new OracleCommand("SELECT NVL(MAX(RECORD_ID),0) FROM BORROWRECORD", conn);
                var maxRecordId = Convert.ToInt32(await cmdMaxRecordId.ExecuteScalarAsync());
                var newRecordId = maxRecordId + 1;

                int finalDays = BorrowRuleCalculator.CalculateBorrowDays(role, creditScore, _cfg);
                var borrowDate = DateTime.Now;
                var dueDate = borrowDate.AddDays(finalDays);
                using var cmdInsert = new OracleCommand("INSERT INTO BORROWRECORD (RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, STATUS, RENEW_TIMES) VALUES (:recordId,:userId,:copyId,:borrowDate,:dueDate,'lending',0)", conn);
                cmdInsert.Parameters.Add("recordId", newRecordId);
                cmdInsert.Parameters.Add("userId", int.Parse(userId));
                cmdInsert.Parameters.Add("copyId", copyId);
                cmdInsert.Parameters.Add("borrowDate", borrowDate);
                cmdInsert.Parameters.Add("dueDate", dueDate);
                await cmdInsert.ExecuteNonQueryAsync();

                using var cmdUpdateCopy = new OracleCommand("UPDATE COPY SET STATUS='BORROWED' WHERE COPY_ID=:copyId", conn);
                cmdUpdateCopy.Parameters.Add("copyId", copyId);
                await cmdUpdateCopy.ExecuteNonQueryAsync();

                TempData["Message"] = $"借阅成功！{message}应在{finalDays}天内归还。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"借阅失败: {ex.Message}";
            }
            return RedirectToPage(new { id = bookId });
        }

        public async Task<IActionResult> OnPostReserveAsync(int? bookId)
        {
            if (bookId == null)
            {
                TempData["ErrorMessage"] = "无效的图书ID。";
                return RedirectToPage(new { id = bookId });
            }
            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                await conn.OpenAsync();
                var userId = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "用户未登录。";
                    return RedirectToPage(new { id = bookId });
                }
                using var cmdMaxReservationId = new OracleCommand("SELECT NVL(MAX(reservation_id),0) FROM Reservation", conn);
                var maxReservationId = Convert.ToInt32(await cmdMaxReservationId.ExecuteScalarAsync());
                var newReservationId = maxReservationId + 1;
                using var cmdMaxQueue = new OracleCommand("SELECT NVL(MAX(queue_position),0) FROM Reservation WHERE book_id = :bid AND status IN ('pending','notified')", conn);
                cmdMaxQueue.Parameters.Add("bid", bookId);
                var maxQueue = Convert.ToInt32(await cmdMaxQueue.ExecuteScalarAsync());
                var newQueuePos = maxQueue + 1;
                var now = DateTime.Now.Date;
                var expiry = now.AddDays(30);
                using var cmdInsert = new OracleCommand("INSERT INTO Reservation (reservation_id,user_id,book_id,reservation_date,status,expiry_date,queue_position) VALUES (:reservation_id,:user_id,:book_id,:reservation_date,'pending',:expiry_date,:queue_position)", conn);
                cmdInsert.Parameters.Add("reservation_id", newReservationId);
                cmdInsert.Parameters.Add("user_id", int.Parse(userId));
                cmdInsert.Parameters.Add("book_id", bookId);
                cmdInsert.Parameters.Add("reservation_date", now);
                cmdInsert.Parameters.Add("expiry_date", expiry);
                cmdInsert.Parameters.Add("queue_position", newQueuePos);
                await cmdInsert.ExecuteNonQueryAsync();
                TempData["Message"] = "预约成功！您已加入预约队列。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"预约失败: {ex.Message}";
            }
            return RedirectToPage(new { id = bookId });
        }

        public class Comment
        {
            public int CommentId { get; set; }
            public string Content { get; set; } = "";
            public int Rating { get; set; }
            public DateTime CommentDate { get; set; }
            public int UserId { get; set; }
            public string Username { get; set; } = "";
        }
    }
}