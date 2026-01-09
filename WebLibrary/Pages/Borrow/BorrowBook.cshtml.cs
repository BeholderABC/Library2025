using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using WebLibrary.Pages.Shared.Models;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Borrow
{
    public class BorrowBookModel : PageModel
    {
        private readonly IConfiguration _c;
        public BorrowBookModel(IConfiguration c) => _c = c;

        [BindProperty] public string BookISBN { get; set; } = "";
        public void OnGet()
        {
        }

        public void OnPost()
        {
            if (!string.IsNullOrEmpty(BookISBN))
            {
                using var conn = new Oracle.ManagedDataAccess.Client.OracleConnection(_c.GetConnectionString("OracleDb"));
                conn.Open();

                using var cmd_find_largest_record_id = new Oracle.ManagedDataAccess.Client.OracleCommand(
                    "SELECT MAX(RECORD_ID) FROM BORROWRECORD", conn);
                var largestRecordId = cmd_find_largest_record_id.ExecuteScalar();
                var newRecordId = largestRecordId != DBNull.Value ? Convert.ToInt32(largestRecordId) + 1 : 1;
                var userId = User.FindFirst("UserId")?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    TempData["ErrorMessage"] = "用户未登录。";
                    return;
                }

                // 1. 获取用户角色和信用分
                string role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Student";
                string? creditStr = User.FindFirst("CreditScore")?.Value;
                int creditScore = string.IsNullOrEmpty(creditStr) ? 60 : int.Parse(creditStr);

                // 2. 检查借阅资格
                var (canBorrow, maxBooks, currentBorrows, message) = BorrowLimitChecker.CheckBorrowingEligibility(
                    int.Parse(userId), role, creditScore, _c);
                
                if (!canBorrow)
                {
                    TempData["ErrorMessage"] = message;
                    return;
                }

                // 2.5. 查找图书ID
                using var cmd_find_book_id = new Oracle.ManagedDataAccess.Client.OracleCommand(
                    "SELECT BOOK_ID FROM BOOK WHERE ISBN = :isbn", conn);
                cmd_find_book_id.Parameters.Add("isbn", BookISBN);
                var bookId = cmd_find_book_id.ExecuteScalar();
                
                if (bookId == null)
                {
                    TempData["ErrorMessage"] = "未找到该ISBN对应的图书。";
                    return;
                }

                // 2.6. 检查用户是否已经借阅了这本书
                using var cmd_check_existing = new Oracle.ManagedDataAccess.Client.OracleCommand(@"
                    SELECT COUNT(*) 
                    FROM BorrowRecord br 
                    JOIN Copy c ON br.copy_id = c.copy_id 
                    WHERE c.book_id = :bookId 
                      AND br.user_id = :userId 
                      AND br.status IN ('lending', 'fined')", conn);
                cmd_check_existing.Parameters.Add("bookId", bookId);
                cmd_check_existing.Parameters.Add("userId", int.Parse(userId));
                var existingBorrows = Convert.ToInt32(cmd_check_existing.ExecuteScalar());
                
                if (existingBorrows > 0)
                {
                    TempData["ErrorMessage"] = "您已经借阅了这本书，不能重复借阅同一本书。";
                    return;
                }

                var borrowDate = DateTime.Now;

                // 使用 BorrowRuleCalculator 计算借阅天数
                int finalDays = BorrowRuleCalculator.CalculateBorrowDays(role, creditScore, _c);

                // 计算到期日期
                var dueDate = DateTime.Now.AddDays(finalDays);



                var lastFinedDate = DateTime.Now; // Default to now, can be updated later if needed
                var status = "lending";
                var renewTimes = 0;
                
                // 使用行锁查找并锁定可用副本（防止并发问题）
                using var cmd_find_available_copy = new Oracle.ManagedDataAccess.Client.OracleCommand(
                    "SELECT COPY_ID FROM COPY WHERE BOOK_ID = :bookId AND STATUS = 'AVAILABLE' AND rownum = 1 FOR UPDATE", conn);
                cmd_find_available_copy.Parameters.Add("bookId", bookId);
                var copyId = cmd_find_available_copy.ExecuteScalar();

                if (copyId != null) // ����BORROWRECORD����COPY���� BOOK��
                {
                    using var cmd_insert_record = new Oracle.ManagedDataAccess.Client.OracleCommand(
                        "INSERT INTO BORROWRECORD (RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, LAST_FINED_DATE, STATUS, RENEW_TIMES) " +
                        "VALUES (:recordId, :userId, :copyId, :borrowDate, :dueDate, :lastFinedDate, :status, :renewTimes)", conn);
                    cmd_insert_record.Parameters.Add("recordId", newRecordId);
                    cmd_insert_record.Parameters.Add("userId", int.Parse(userId!));
                    cmd_insert_record.Parameters.Add("copyId", int.Parse(copyId.ToString()!));
                    cmd_insert_record.Parameters.Add("borrowDate", borrowDate);
                    cmd_insert_record.Parameters.Add("dueDate", dueDate);
                    cmd_insert_record.Parameters.Add("lastFinedDate", lastFinedDate);
                    cmd_insert_record.Parameters.Add("status", status);
                    cmd_insert_record.Parameters.Add("renewTimes", renewTimes);
                    cmd_insert_record.ExecuteNonQuery();
                    using var cmd_update_copy_status = new Oracle.ManagedDataAccess.Client.OracleCommand(
                        "UPDATE COPY SET status = 'BORROWED' WHERE copy_id = :copyId", conn);
                    cmd_update_copy_status.Parameters.Add("copyId", int.Parse(copyId.ToString()!));
                    cmd_update_copy_status.ExecuteNonQuery();
                    //using var com_update_available_copy = new Oracle.ManagedDataAccess.Client.OracleCommand(
                    //    "UPDATE BOOK SET AVAILABLE_COPIES = AVAILABLE_COPIES - 1 WHERE BOOK_ID = :bookId", conn);
                    //com_update_available_copy.Parameters.Add("bookId", int.Parse(bookId.ToString()!));
                    //com_update_available_copy.ExecuteNonQuery();
                    
                    

                    TempData["Message"] = $"借书成功！{message}应在{finalDays}天内归还图书。";
                }
                else
                {
                    TempData["ErrorMessage"] = "该图书当前没有可借阅的副本，请稍后再试或选择其他图书。";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "请输入有效的ISBN号。";
            }
        }
    }
}
