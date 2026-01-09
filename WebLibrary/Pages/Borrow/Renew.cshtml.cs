using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using WebLibrary.Services;

namespace WebLibrary.Pages.Borrow
{
    [Authorize]
    public class RenewModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly BorrowService _borrowService;

        public RenewModel(IConfiguration config, BorrowService borrowService)
        {
            _config = config;
            _borrowService = borrowService;
        }

        public List<BorrowRecordVM> BorrowRecords { get; set; } = new List<BorrowRecordVM>();

        public class BorrowRecordVM
        {
            public int RecordId { get; set; }
            public string BookTitle { get; set; } = string.Empty;
            public string Author { get; set; } = string.Empty;
            public string CopyId { get; set; } = string.Empty;
            public int BookId { get; set; }
            public DateTime BorrowDate { get; set; }
            public DateTime DueDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public int RenewTimes { get; set; }
            public bool CanRenew { get; set; }
            public string RenewMessage { get; set; } = string.Empty;
            public bool IsOverdue { get; set; }
        }

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return;

            LoadBorrowRecords(int.Parse(userId));
        }

        // 续借图书
        public IActionResult OnPostRenewBook(int recordId)
        {
            try
            {
                var userId = User.FindFirst("UserId")?.Value;
                if (userId == null)
                {
                    TempData["ErrorMessage"] = "用户未登录或会话已过期";
                    return RedirectToPage("/Account/Login");
                }

                // 验证recordId是否有效
                if (recordId <= 0)
                {
                    TempData["ErrorMessage"] = "无效的借阅记录ID";
                    return RedirectToPage();
                }

                var result = _borrowService.RenewBorrowRecord(recordId, int.Parse(userId));

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                }
                else
                {
                    TempData["ErrorMessage"] = result.Message;
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"续借过程中发生错误：{ex.Message}";
            }

            return RedirectToPage();
        }

        private void LoadBorrowRecords(int userId)
        {
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(
                @"SELECT 
                    br.record_id, 
                    b.title, 
                    b.author,
                    br.copy_id, 
                    b.book_id,
                    br.borrow_date, 
                    br.due_date, 
                    br.status, 
                    br.renew_times
                  FROM BorrowRecord br
                  JOIN Copy c ON br.copy_id = c.copy_id
                  JOIN Book b ON c.book_id = b.book_id
                  WHERE br.user_id = :userId
                    AND br.status IN ('lending', 'overdue')
                  ORDER BY br.due_date ASC", conn);

            cmd.Parameters.Add("userId", userId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var dueDate = reader.GetDateTime(6);
                var status = reader.GetString(7);
                var isOverdue = status == "overdue" ||
                                (status == "lending" && dueDate < DateTime.Today);

                var record = new BorrowRecordVM
                {
                    RecordId = reader.GetInt32(0),
                    BookTitle = reader.GetString(1),
                    Author = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CopyId = reader.GetString(3),
                    BookId = reader.GetInt32(4),
                    BorrowDate = reader.GetDateTime(5),
                    DueDate = dueDate,
                    Status = status,
                    RenewTimes = reader.GetInt32(8),
                    IsOverdue = isOverdue
                };

                CalculateRenewEligibility(record);
                BorrowRecords.Add(record);
            }
        }

        private void CalculateRenewEligibility(BorrowRecordVM record)
        {
            var today = DateTime.Today;
            var earliestRenewDate = record.DueDate.AddDays(-10);

            // 检查续借条件
            bool canRenew = record.Status == "lending" &&
                            record.RenewTimes < 2 &&
                            today >= earliestRenewDate &&
                            !record.IsOverdue;

            record.CanRenew = canRenew;

            if (!record.CanRenew)
            {
                if (record.Status != "lending")
                {
                    record.RenewMessage = "图书已归还";
                }
                else if (record.RenewTimes >= 2)
                {
                    record.RenewMessage = "已达续借上限";
                }
                else if (today < earliestRenewDate)
                {
                    record.RenewMessage = $"续借时间未到，最早 {earliestRenewDate:yyyy-MM-dd} 可续借";
                }
                else if (record.IsOverdue)
                {
                    record.RenewMessage = "图书已逾期，请先归还";
                }
                else
                {
                    record.RenewMessage = "不符合续借条件";
                }
            }
        }
    }
} 