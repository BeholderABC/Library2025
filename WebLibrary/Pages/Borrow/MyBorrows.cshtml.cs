using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Borrow
{
    [Authorize]
    public class MyBorrowsModel : PageModel
    {
        private readonly IConfiguration _config;

        public MyBorrowsModel(IConfiguration config)
        {
            _config = config;
        }

        public List<BorrowRecordVM> BorrowRecords { get; set; } = new List<BorrowRecordVM>();

        public class BorrowRecordVM
        {
            public int RecordId { get; set; }
            public string BookTitle { get; set; }
            public string CopyId { get; set; }
            public int BookId { get; set; }
            public DateTime BorrowDate { get; set; }
            public DateTime DueDate { get; set; }
            public string Status { get; set; }
            public int RenewTimes { get; set; }
            public bool CanRenew { get; set; }
            public string RenewMessage { get; set; }
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
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return RedirectToPage("/Account/Login");

            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            try
            {
                // 1. 获取借阅记录
                using var getCmd = new OracleCommand(
                    "SELECT due_date, renew_times, status FROM BorrowRecord " +
                    "WHERE record_id = :id AND user_id = :userId", conn);
                getCmd.Parameters.Add("id", recordId);
                getCmd.Parameters.Add("userId", int.Parse(userId));

                DateTime dueDate;
                int renewTimes;
                string status;

                using (var reader = getCmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        TempData["ErrorMessage"] = "借阅记录不存在";
                        return RedirectToPage();
                    }

                    dueDate = reader.GetDateTime(0);
                    renewTimes = reader.GetInt32(1);
                    status = reader.GetString(2);
                }

                // 2. 验证续借条件
                if (status != "lending")
                {
                    TempData["ErrorMessage"] = "只有借阅中的图书才能续借";
                    return RedirectToPage();
                }

                if (renewTimes >= 2)
                {
                    TempData["ErrorMessage"] = "已达到续借上限（最多2次）";
                    return RedirectToPage();
                }

                var earliestRenewDate = dueDate.AddDays(-10);
                if (DateTime.Today < earliestRenewDate)
                {
                    TempData["ErrorMessage"] = $"续借时间未到，最早 {earliestRenewDate:yyyy-MM-dd} 可续借";
                    return RedirectToPage();
                }

                // 3. 执行续借
                var newDueDate = dueDate.AddDays(15);
                using var updateCmd = new OracleCommand(
                    "UPDATE BorrowRecord " +
                    "SET due_date = :newDue, renew_times = renew_times + 1 " +
                    "WHERE record_id = :id", conn);

                updateCmd.Parameters.Add("newDue", newDueDate);
                updateCmd.Parameters.Add("id", recordId);
                updateCmd.ExecuteNonQuery();

                TempData["SuccessMessage"] = $"续借成功！新应还日期: {newDueDate:yyyy-MM-dd}";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"续借失败: {ex.Message}";
                return RedirectToPage();
            }
        }

        private void LoadBorrowRecords(int userId)
        {
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = new OracleCommand(
                @"SELECT 
                    br.record_id, 
                    b.title, 
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
                  ORDER BY br.due_date ASC", conn);

            cmd.Parameters.Add("userId", userId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var dueDate = reader.GetDateTime(5);
                var status = reader.GetString(6);
                var isOverdue = status == "overdue" ||
                                (status == "lending" && dueDate < DateTime.Today);

                var record = new BorrowRecordVM
                {
                    RecordId = reader.GetInt32(0),
                    BookTitle = reader.GetString(1),
                    CopyId = reader.GetString(2),
                    BookId = reader.GetInt32(3),
                    BorrowDate = reader.GetDateTime(4),
                    DueDate = dueDate,
                    Status = status,
                    RenewTimes = reader.GetInt32(7),
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
                            today >= earliestRenewDate;

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
                    record.RenewMessage = "逾期图书请先归还，然后重新借阅";
                }
                else
                {
                    record.RenewMessage = "不符合续借条件";
                }
            }
        }
    }
}