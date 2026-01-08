using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Admin
{
    public class UserPayFineModel : PageModel
    {
        private readonly IConfiguration _c;
        public UserPayFineModel(IConfiguration c) => _c = c;
        
        [BindProperty]
        public string SearchUserId { get; set; } = "";
        
        [BindProperty]
        public string SearchUserName { get; set; } = "";
        
        [BindProperty]
        public int CurrentUserId { get; set; } = 0;
        
        public List<UserBorrowingBook> UserBorrowingBooks { get; set; } = new();
        public UserInfo? SelectedUser { get; set; }

        public void OnGet()
        {
            // Empty on initial load
        }

        public async Task<IActionResult> OnPostSearchUserAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SearchUserId) && string.IsNullOrWhiteSpace(SearchUserName))
                {
                    TempData["ErrorMessage"] = "请输入用户ID或用户名进行搜索";
                    return Page();
                }

                using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // Search for user
                string userQuery;
                OracleCommand userCmd;
                
                if (!string.IsNullOrWhiteSpace(SearchUserId))
                {
                    // 用户ID搜索需要是数字，增加健壮性校验
                    if (!int.TryParse(SearchUserId, out var parsedUserId))
                    {
                        TempData["ErrorMessage"] = "用户ID必须是数字";
                        return Page();
                    }
                    userQuery = "SELECT USER_ID, USER_NAME, EMAIL FROM Users WHERE USER_ID = :searchValue";
                    userCmd = new OracleCommand(userQuery, conn);
                    userCmd.Parameters.Add("searchValue", parsedUserId);
                }
                else
                {
                    userQuery = "SELECT USER_ID, USER_NAME, EMAIL FROM Users WHERE UPPER(USER_NAME) = UPPER(:searchValue)";
                    userCmd = new OracleCommand(userQuery, conn);
                    userCmd.Parameters.Add("searchValue", SearchUserName);
                }

                using var userReader = await userCmd.ExecuteReaderAsync();
                if (await userReader.ReadAsync())
                {
                    CurrentUserId = Convert.ToInt32(userReader["USER_ID"]);
                    SelectedUser = new UserInfo
                    {
                        UserId = CurrentUserId,
                        UserName = userReader["USER_NAME"].ToString() ?? "",
                        Email = userReader["EMAIL"].ToString() ?? ""
                    };
                }
                else
                {
                    TempData["ErrorMessage"] = "未找到匹配的用户";
                    return Page();
                }
                userReader.Close();
                userCmd.Dispose();
                await LoadUserBorrowingDataAsync(conn, CurrentUserId);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"搜索失败：{ex.Message}";
            }
            
            return Page();
        }

        private async Task LoadUserBorrowingDataAsync(OracleConnection conn, int userId)
        {
            UserBorrowingBooks.Clear();
            
            // 初始化该用户尚未设置 LAST_FINED_DATE 的记录，使其与 DUE_DATE 对齐（仅限当前用户且仍在借/逾期）
            using (var cmdUpdateRecord = new OracleCommand(
                       "UPDATE BORROWRECORD SET LAST_FINED_DATE = DUE_DATE " +
                       "WHERE LAST_FINED_DATE IS NULL AND USER_ID = :userId AND STATUS IN ('lending','overdue')", conn))
            {
                cmdUpdateRecord.Parameters.Add("userId", userId);
                await cmdUpdateRecord.ExecuteNonQueryAsync();
            }

            // 先查看这个用户的所有借阅记录
            System.Diagnostics.Debug.WriteLine($"=== 管理员页面 - 用户 {userId} 的所有借阅记录 ===");
            using var cmdDebug = new OracleCommand(
                @"SELECT b.title, br.due_date, br.record_id, br.copy_id, br.status, br.last_fined_date, br.borrow_date
                  FROM BorrowRecord br
                  JOIN Copy c ON br.copy_id = c.copy_id
                  JOIN Book b ON c.book_id = b.book_id
                  WHERE br.user_id = :userId
                  ORDER BY br.due_date ASC", conn);
            cmdDebug.Parameters.Add("userId", userId);
            using var debugReader = await cmdDebug.ExecuteReaderAsync();
            while (await debugReader.ReadAsync())
            {
                System.Diagnostics.Debug.WriteLine($"书名: {debugReader["title"]}, 到期日: {debugReader["due_date"]}, 状态: {debugReader["status"]}, 最后罚款日期: {debugReader["last_fined_date"]}, 借阅日期: {debugReader["borrow_date"]}");
            }
            debugReader.Close();

            // Get user info
            using var userCmd = new OracleCommand("SELECT USER_ID, USER_NAME, EMAIL FROM Users WHERE USER_ID = :userId", conn);
            userCmd.Parameters.Add("userId", userId);
            using var userReader = await userCmd.ExecuteReaderAsync();
            if (await userReader.ReadAsync())
            {
                SelectedUser = new UserInfo
                {
                    UserId = Convert.ToInt32(userReader["USER_ID"]),
                    UserName = userReader["USER_NAME"].ToString() ?? "",
                    Email = userReader["EMAIL"].ToString() ?? ""
                };
            }
            userReader.Close();

            // Get user's borrowed books
            using var cmd = new OracleCommand(
                @"SELECT b.title, br.due_date, br.record_id, br.copy_id
                  FROM BorrowRecord br
                  JOIN Copy c ON br.copy_id = c.copy_id
                  JOIN Book b ON c.book_id = b.book_id
                  WHERE br.user_id = :userId AND (br.status = 'lending' OR br.status = 'overdue')
                  ORDER BY br.due_date ASC", conn);
            cmd.Parameters.Add("userId", userId);
            
            System.Diagnostics.Debug.WriteLine($"=== 管理员页面 - 用户 {userId} 符合条件的借阅记录 ===");
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dueDate = Convert.ToDateTime(reader["due_date"]);
                var copyId = Convert.ToInt32(reader["copy_id"]);
                var recordId = Convert.ToInt32(reader["record_id"]);
                var title = reader["title"].ToString();
                
                System.Diagnostics.Debug.WriteLine($"处理记录: 书名={title}, 到期日={dueDate:yyyy-MM-dd}, CopyId={copyId}, RecordId={recordId}");
                
                var daysLeft = (dueDate - DateTime.Now).Days;
                string status = daysLeft <= 3 ? $"{daysLeft} 天后到期" : "正常";
                if (daysLeft < 0)
                {
                    status = "已逾期";
                }
                else if (daysLeft == 0)
                {
                    status = "今天到期";
                }

                // 只计算一次罚金，避免重复数据库访问
                var fine = await CalculateFineAsync(recordId, conn);
                
                UserBorrowingBooks.Add(new UserBorrowingBook
                {
                    BookTitle = reader["title"].ToString() ?? "",
                    DueDate = dueDate,
                    Delay = DateTime.Now - dueDate > TimeSpan.Zero ? DateTime.Now - dueDate : TimeSpan.Zero,
                    Status = status,
                    RecordId = Convert.ToInt32(reader["record_id"]),
                    CopyId = Convert.ToInt32(reader["copy_id"]),
                    Fine = fine,
                });
                
                System.Diagnostics.Debug.WriteLine($"罚款计算结果: {fine} 元");
            }

            // 便于页面回填搜索框
            SearchUserId = userId.ToString();
        }

        public async Task<IActionResult> OnPostPayFineAsync(int copyId, int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    TempData["ErrorMessage"] = "用户信息无效";
                    return RedirectToPage();
                }

                CurrentUserId = userId;

                using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // 首先通过copyId找到对应的recordId
                using var cmdGetRecordId = new OracleCommand(
                    "SELECT RECORD_ID FROM BORROWRECORD WHERE COPY_ID = :copyId AND USER_ID = :userId AND STATUS IN ('lending', 'overdue')", conn);
                cmdGetRecordId.Parameters.Add("copyId", copyId);
                cmdGetRecordId.Parameters.Add("userId", userId);
                var recordIdObj = await cmdGetRecordId.ExecuteScalarAsync();
                
                if (recordIdObj == null)
                {
                    TempData["ErrorMessage"] = "未找到对应的借阅记录";
                    await LoadUserBorrowingDataAsync(conn, CurrentUserId);
                    return Page();
                }
                
                int recordId = Convert.ToInt32(recordIdObj);
                var fine = await CalculateFineAsync(recordId, conn);
                if (fine <= 0)
                {
                    TempData["ErrorMessage"] = "该图书无罚金需要支付。";
                    await LoadUserBorrowingDataAsync(conn, CurrentUserId);
                    return Page();
                }

                // Update the last fined date to mark fine as paid
                using var cmdUpdateRecord = new OracleCommand(
                    "UPDATE BORROWRECORD SET LAST_FINED_DATE = :nowDate WHERE COPY_ID = :copyId AND USER_ID = :userId AND STATUS IN ('lending', 'overdue')", conn);
                // 使用 DateTime.Today 保持与计算逻辑（按日）一致，避免同一天时间差导致的边界问题
                cmdUpdateRecord.Parameters.Add("nowDate", DateTime.Today);
                cmdUpdateRecord.Parameters.Add("copyId", copyId);
                cmdUpdateRecord.Parameters.Add("userId", userId);
                
                int rowsAffected = await cmdUpdateRecord.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    TempData["Message"] = $"成功为用户支付罚金 {fine} 元。";
                }
                else
                {
                    TempData["ErrorMessage"] = "未找到对应的借阅记录";
                }

                // Reload user data to refresh the display
                await LoadUserBorrowingDataAsync(conn, CurrentUserId);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"支付罚金失败：{ex.Message}";
            }
            
            return Page();
        }

        public async Task<IActionResult> OnPostReturnBookAsync(int recordId, int copyId, int userId)
        {
            try
            {
                if (userId <= 0)
                {
                    TempData["ErrorMessage"] = "用户信息无效";
                    return RedirectToPage();
                }

                CurrentUserId = userId;

                using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // Verify the borrow record belongs to the specified user
                using var cmdVerify = new OracleCommand(
                    "SELECT 1 FROM BORROWRECORD WHERE RECORD_ID = :recordId AND USER_ID = :userId AND (STATUS = 'lending' OR STATUS = 'overdue')", conn);
                cmdVerify.Parameters.Add("recordId", recordId);
                cmdVerify.Parameters.Add("userId", userId);
                var exists = await cmdVerify.ExecuteScalarAsync();

                if (exists == null)
                {
                    TempData["ErrorMessage"] = "未找到对应的借阅记录";
                    await LoadUserBorrowingDataAsync(conn, CurrentUserId);
                    return Page();
                }

                // Check if there's outstanding fine
                var fine = await CalculateFineAsync(recordId, conn);
                if (fine > 0)
                {
                    TempData["ErrorMessage"] = $"请先支付罚金 {fine} 元后再归还图书。";
                    await LoadUserBorrowingDataAsync(conn, CurrentUserId);
                    return Page();
                }

                // Update borrow record status to 'returned' and set return date
                using var cmdUpdateRecord = new OracleCommand(
                    "UPDATE BORROWRECORD SET STATUS = 'returned', RETURN_DATE = :returnDate WHERE RECORD_ID = :recordId", conn);
                cmdUpdateRecord.Parameters.Add("returnDate", DateTime.Now);
                cmdUpdateRecord.Parameters.Add("recordId", recordId);
                await cmdUpdateRecord.ExecuteNonQueryAsync();

                // Handle reservation queue logic
                // 1. Get book_id
                using var cmdGetBookId = new OracleCommand("SELECT BOOK_ID FROM COPY WHERE COPY_ID = :copyId", conn);
                cmdGetBookId.Parameters.Add("copyId", copyId);
                var bookIdObj = await cmdGetBookId.ExecuteScalarAsync();
                if (bookIdObj == null)
                {
                    TempData["ErrorMessage"] = "未找到对应的图书信息，无法处理预约队列。";
                    await LoadUserBorrowingDataAsync(conn, CurrentUserId);
                    return Page();
                }
                int bookId = Convert.ToInt32(bookIdObj);

                // 2. Get first reservation in queue
                using var cmdGetReservation = new OracleCommand(
                    "SELECT reservation_id, user_id FROM Reservation WHERE book_id = :bookId AND status = 'pending' ORDER BY queue_position ASC FETCH FIRST 1 ROWS ONLY", conn);
                cmdGetReservation.Parameters.Add("bookId", bookId);
                using var readerReservation = await cmdGetReservation.ExecuteReaderAsync();
                if (await readerReservation.ReadAsync())
                {
                    int reservationId = Convert.ToInt32(readerReservation["reservation_id"]);
                    int reservedUserId = Convert.ToInt32(readerReservation["user_id"]);
                    readerReservation.Close();

                    // 3.1 Set reservation status to fulfilled
                    using var cmdUpdateReservation = new OracleCommand(
                        "UPDATE Reservation SET status = 'fulfilled' WHERE reservation_id = :reservationId", conn);
                    cmdUpdateReservation.Parameters.Add("reservationId", reservationId);
                    await cmdUpdateReservation.ExecuteNonQueryAsync();

                    // 3.2 Insert new borrow record
                    using var cmdGetMaxRecordId = new OracleCommand("SELECT NVL(MAX(RECORD_ID),0) FROM BORROWRECORD", conn);
                    int newRecordId = Convert.ToInt32(await cmdGetMaxRecordId.ExecuteScalarAsync()) + 1;
                    var now = DateTime.Now;
                    var due = now.AddDays(30);
                    using var cmdInsertBorrow = new OracleCommand(
                        "INSERT INTO BORROWRECORD (RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, STATUS, RENEW_TIMES, RETURN_DATE, LAST_FINED_DATE) " +
                        "VALUES (:recordId, :userId, :copyId, :borrowDate, :dueDate, :status, :renewTimes, :returnDate, :lastFinedDate)", conn);
                    cmdInsertBorrow.Parameters.Add("recordId", OracleDbType.Int32).Value = newRecordId;
                    cmdInsertBorrow.Parameters.Add("userId", OracleDbType.Int32).Value = reservedUserId;
                    cmdInsertBorrow.Parameters.Add("copyId", OracleDbType.Int32).Value = copyId;
                    cmdInsertBorrow.Parameters.Add("borrowDate", OracleDbType.Date).Value = now;
                    cmdInsertBorrow.Parameters.Add("dueDate", OracleDbType.Date).Value = due;
                    cmdInsertBorrow.Parameters.Add("status", OracleDbType.Varchar2).Value = "lending";
                    cmdInsertBorrow.Parameters.Add("renewTimes", OracleDbType.Int32).Value = 0;
                    cmdInsertBorrow.Parameters.Add("returnDate", OracleDbType.Date).Value = DBNull.Value;
                    cmdInsertBorrow.Parameters.Add("lastFinedDate", OracleDbType.Date).Value = DBNull.Value;
                    await cmdInsertBorrow.ExecuteNonQueryAsync();

                    // Update remaining pending reservations' queue_position
                    using var cmdUpdateQueue = new OracleCommand(
                        "UPDATE Reservation SET queue_position = queue_position - 1 WHERE book_id = :bookId AND status = 'pending' AND queue_position > 1 AND queue_position IS NOT NULL", conn);
                    cmdUpdateQueue.Parameters.Add("bookId", bookId);
                    await cmdUpdateQueue.ExecuteNonQueryAsync();

                    TempData["Message"] = "归还成功，已自动借阅给预约队首用户。";
                }
                else
                {
                    // No reservations, set copy to AVAILABLE
                    using var cmdUpdateCopy = new OracleCommand(
                        "UPDATE COPY SET STATUS = 'AVAILABLE' WHERE COPY_ID = :copyId", conn);
                    cmdUpdateCopy.Parameters.Add("copyId", copyId);
                    await cmdUpdateCopy.ExecuteNonQueryAsync();

                    TempData["Message"] = "归还成功。";
                }

                // Reload user data to refresh the display
                await LoadUserBorrowingDataAsync(conn, CurrentUserId);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"归还失败：{ex.Message}";
            }
            
            return Page();
        }

        private async Task<int> CalculateFineAsync(int recordId, OracleConnection conn)
        {
            using var cmdGetLastFinedDate = new OracleCommand(
                "SELECT LAST_FINED_DATE, DUE_DATE FROM BORROWRECORD WHERE RECORD_ID = :recordId AND STATUS IN ('lending', 'overdue')", conn);
            cmdGetLastFinedDate.Parameters.Add("recordId", recordId);

            using var reader = await cmdGetLastFinedDate.ExecuteReaderAsync();
            DateTime lastFinedDate = DateTime.MinValue;
            DateTime dueDate = DateTime.MinValue;
            
            if (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0))
                    lastFinedDate = reader.GetDateTime(0);
                dueDate = reader.GetDateTime(1);
            }
            else
            {
                return 0;
            }

            // 使用新的罚款计算器
            if (lastFinedDate == DateTime.MinValue || lastFinedDate < dueDate)
            {
                // 如果没有罚金记录或最后罚金日期早于到期日期，计算总罚款
                return (int)FineCalculator.CalculateOverdueFine(dueDate);
            }
            else
            {
                // 计算增量罚款
                return (int)FineCalculator.CalculateIncrementalFine(dueDate, lastFinedDate);
            }
        }

        public class UserBorrowingBook
        {
            public string BookTitle { get; set; } = "";
            public DateTime DueDate { get; set; }
            public TimeSpan Delay { get; set; }
            public string Status { get; set; } = "";
            public int RecordId { get; set; }
            public int CopyId { get; set; }
            public int Fine { get; set; } = 0;
        }

        public class UserInfo
        {
            public int UserId { get; set; }
            public string UserName { get; set; } = "";
            public string Email { get; set; } = "";
        }
    }
}
