using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Borrow
{
    public class ReturnBookModel : PageModel
    {
        private readonly IConfiguration _c;
        public ReturnBookModel(IConfiguration c) => _c = c;
        public List<BorrowingBook> BorrowingBooks { get; set; } = new();

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return;
            using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd_update_record = new OracleCommand(
                "UPDATE BORROWRECORD SET LAST_FINED_DATE = DUE_DATE WHERE LAST_FINED_DATE is NULL", conn);
            cmd_update_record.ExecuteNonQuery();

            // 先查看这个用户的所有借阅记录
            System.Diagnostics.Debug.WriteLine($"=== 用户 {userId} 的所有借阅记录 ===");
            using var cmdDebug = new OracleCommand(
                @"SELECT b.title, br.due_date, br.record_id, br.copy_id, br.status, br.last_fined_date, br.borrow_date
                  FROM BorrowRecord br
                  JOIN Copy c ON br.copy_id = c.copy_id
                  JOIN Book b ON c.book_id = b.book_id
                  WHERE br.user_id = :userId
                  ORDER BY br.due_date ASC", conn);
            cmdDebug.Parameters.Add("userId", int.Parse(userId));
            using var debugReader = cmdDebug.ExecuteReader();
            while (debugReader.Read())
            {
                System.Diagnostics.Debug.WriteLine($"书名: {debugReader["title"]}, 到期日: {debugReader["due_date"]}, 状态: {debugReader["status"]}, 最后罚款日期: {debugReader["last_fined_date"]}, 借阅日期: {debugReader["borrow_date"]}");
            }
            debugReader.Close();

            using var cmd = new OracleCommand(
                @"SELECT b.title, br.due_date, br.record_id, br.copy_idn
                  FROM BorrowRecord br
                  JOIN Copy c ON br.copy_id = c.copy_id
                  JOIN Book b ON c.book_id = b.book_id
                  WHERE br.user_id = :userId AND (br.status = 'lending' OR br.status = 'fined')
                  ORDER BY br.due_date ASC", conn);
            cmd.Parameters.Add("userId", int.Parse(userId));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var dueDate = Convert.ToDateTime(reader["due_date"]);
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
                BorrowingBooks.Add(new BorrowingBook
                {
                    BookTitle = reader["title"].ToString(),
                    DueDate = dueDate,
                    Delay = DateTime.Now - dueDate > TimeSpan.Zero ? DateTime.Now - dueDate : TimeSpan.Zero,
                    Status = status,
                    RecordId = Convert.ToInt32(reader["record_id"]),
                    CopyId = Convert.ToInt32(reader["copy_id"]),
                    Fine = CalculateFine(Convert.ToInt32(reader["copy_id"])),
                });
            }
            conn.Close();
        }

        public async Task<IActionResult> OnPostReturnAsync(int recordId, int copyId)
        {
            try
            {
                var userId = User.FindFirst("UserId")?.Value;
                if (userId == null)
                {
                    TempData["ErrorMessage"] = "用户未登录";
                    return RedirectToPage();
                }

                using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(_c.GetConnectionString("OracleDb"));
                await conn.OpenAsync();

                // 1. 验证借阅记录属于当前用户
                using var cmdVerify = new OracleCommand(
                    "SELECT 1 FROM BORROWRECORD WHERE RECORD_ID = :recordId AND USER_ID = :userId AND (STATUS = 'lending' OR STATUS = 'fined')", conn);
                cmdVerify.Parameters.Add("recordId", recordId);
                cmdVerify.Parameters.Add("userId", int.Parse(userId));
                var exists = await cmdVerify.ExecuteScalarAsync();

                if (exists == null)
                {
                    TempData["ErrorMessage"] = "未找到对应的借阅记录或记录不属于当前用户";
                    return RedirectToPage();
                }

                // 检查是否有罚金需要支付
                if (CalculateFine(copyId) > 0)
                {
                    TempData["ErrorMessage"] = $"请先支付罚金 {CalculateFine(copyId)} 元后再归还图书。";
                    return RedirectToPage();
                }

                // 2. 更新借阅记录状态为'returned'并设置归还日期
                using var cmdUpdateRecord = new OracleCommand(
                    "UPDATE BORROWRECORD SET STATUS = 'returned', RETURN_DATE = :returnDate WHERE RECORD_ID = :recordId", conn);
                cmdUpdateRecord.Parameters.Add("returnDate", DateTime.Now);
                cmdUpdateRecord.Parameters.Add("recordId", recordId);
                await cmdUpdateRecord.ExecuteNonQueryAsync();

                // ========== 预约队列处理逻辑 ========== 
                // 1. 获取book_id
                using var cmdGetBookId = new OracleCommand("SELECT BOOK_ID FROM COPY WHERE COPY_ID = :copyId", conn);
                cmdGetBookId.Parameters.Add("copyId", copyId);
                var bookIdObj = await cmdGetBookId.ExecuteScalarAsync();
                if (bookIdObj == null)
                {
                    TempData["ErrorMessage"] = "未找到对应的图书信息，无法处理预约队列。";
                    return RedirectToPage();
                }
                int bookId = Convert.ToInt32(bookIdObj);

                // 2. 获取队首预约
                using var cmdGetReservation = new OracleCommand(
                    "SELECT reservation_id, user_id FROM Reservation WHERE book_id = :bookId AND status = 'pending' ORDER BY queue_position ASC FETCH FIRST 1 ROWS ONLY", conn);
                cmdGetReservation.Parameters.Add("bookId", bookId);
                using var readerReservation = await cmdGetReservation.ExecuteReaderAsync();
                if (await readerReservation.ReadAsync())
                {
                    int reservationId = Convert.ToInt32(readerReservation["reservation_id"]);
                    int reservedUserId = Convert.ToInt32(readerReservation["user_id"]);
                    readerReservation.Close();

                    // 3.1 设置预约状态为fulfilled
                    using var cmdUpdateReservation = new OracleCommand(
                        "UPDATE Reservation SET status = 'fulfilled' WHERE reservation_id = :reservationId", conn);
                    cmdUpdateReservation.Parameters.Add("reservationId", reservationId);
                    await cmdUpdateReservation.ExecuteNonQueryAsync();

                    // 3.2 插入借阅记录
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

                    // 更新剩余pending预约的queue_position
                    using var cmdUpdateQueue = new OracleCommand(
                        "UPDATE Reservation SET queue_position = queue_position - 1 WHERE book_id = :bookId AND status = 'pending' AND queue_position > 1 AND queue_position IS NOT NULL", conn);
                    cmdUpdateQueue.Parameters.Add("bookId", bookId);
                    await cmdUpdateQueue.ExecuteNonQueryAsync();

                    TempData["Message"] = "归还成功，已自动借阅给预约队首用户。";
                }
                else
                {
                    // 没有预约，将copy设为AVAILABLE
                    using var cmdUpdateCopy = new OracleCommand(
                        "UPDATE COPY SET STATUS = 'AVAILABLE' WHERE COPY_ID = :copyId", conn);
                    cmdUpdateCopy.Parameters.Add("copyId", copyId);
                    await cmdUpdateCopy.ExecuteNonQueryAsync();

                    TempData["Message"] = "归还成功。";
                }
                // ========== 预约队列处理逻辑结束 ==========

            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"归还失败：{ex.Message}";
            }
            return RedirectToPage();
        }

        public IActionResult OnPostGoToPayFine(int copyId, int fine)
        {
            try
            {
                var userId = User.FindFirst("UserId")?.Value;
                if (userId == null)
                {
                    TempData["ErrorMessage"] = "用户未登录";
                    return RedirectToPage();
                }
                if (CalculateFine(copyId) <= 0)
                {
                    TempData["ErrorMessage"] = "无罚金需要支付。";
                    return RedirectToPage();
                }
                TempData["Message"] = $"您需支付 {CalculateFine(copyId)} 元，请前往一楼大厅完成支付。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"失败：{ex.Message}";
                return RedirectToPage();
            }
            return RedirectToPage();
        }


        public IActionResult OnPostPayFine(int copyId, int fine)
        {
            try
            {
                var userId = User.FindFirst("UserId")?.Value;
                if (userId == null)
                {
                    TempData["ErrorMessage"] = "用户未登录";
                    return RedirectToPage();
                }
                if (CalculateFine(copyId) <= 0)
                {
                    TempData["ErrorMessage"] = "无罚金需要支付。";
                    return RedirectToPage();
                }
                using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(_c.GetConnectionString("OracleDb"));
                conn.Open();

                var cmd_update_old_record = new Oracle.ManagedDataAccess.Client.OracleCommand(
                    "UPDATE BORROWRECORD SET LAST_FINED_DATE = :nowDate WHERE COPY_ID = :copyId AND STATUS = 'lending'", conn);
                cmd_update_old_record.Parameters.Add("nowDate", DateTime.Now);
                cmd_update_old_record.Parameters.Add("copyId", copyId);
                cmd_update_old_record.ExecuteNonQuery();

                conn.Close();

                TempData["Message"] = "支付罚金成功，请尽快归还图书。";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"支付罚金失败：{ex.Message}";
            }
            return RedirectToPage();
        }
        private int CalculateFine(int copy_id)
        {
            using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd_get_last_fined_date = new Oracle.ManagedDataAccess.Client.OracleCommand(
                "SELECT LAST_FINED_DATE, DUE_DATE FROM BORROWRECORD WHERE COPY_ID = :copy_id AND STATUS IN ('lending', 'fined')", conn);
            cmd_get_last_fined_date.Parameters.Add("copy_id", copy_id);

            using var reader = cmd_get_last_fined_date.ExecuteReader();
            DateTime lastFinedDate = DateTime.MinValue;
            DateTime dueDate = DateTime.MinValue;
            if (reader.Read())
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

        public class BorrowingBook
        {
            public string BookTitle { get; set; }
            public DateTime DueDate { get; set; }
            public TimeSpan Delay { get; set; }
            public string Status { get; set; }
            public int RecordId { get; set; }
            public int CopyId { get; set; }
            public int Fine { get; set; } = 0;
        }
    }
}
