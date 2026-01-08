using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualBasic;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel.DataAnnotations;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages.Admin
{
    public class HandleOverdueModel : PageModel
    {
        private readonly string _connectionString;

        public HandleOverdueModel(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("OracleDb");
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public List<OverdueRecord> OverdueRecords { get; set; } = new List<OverdueRecord>();

        public class InputModel
        {
            [Required(ErrorMessage = "请选择借阅记录")]
            [Display(Name = "借阅记录ID")]
            public int RecordId { get; set; }

            [Required(ErrorMessage = "请输入实际归还日期")]
            [Display(Name = "实际归还日期")]
            [DataType(DataType.Date)]
            public DateTime ActualReturnDate { get; set; } = DateTime.Today;

            [Required(ErrorMessage = "请输入罚款金额")]
            [Display(Name = "罚款金额")]
            [Range(0.01, 1000, ErrorMessage = "罚款金额必须在0.01到1000之间")]
            public decimal FineAmount { get; set; }

            [Display(Name = "备注")]
            [StringLength(500, ErrorMessage = "备注不能超过500字符")]
            public string? Remarks { get; set; }
        }

        public class OverdueRecord
        {
            public int RecordId { get; set; }
            public int UserId { get; set; }
            public string UserName { get; set; } = "";
            public string BookTitle { get; set; } = "";
            public int BookId { get; set; }
            public DateTime BorrowDate { get; set; }
            public DateTime DueDate { get; set; }
            public int OverdueDays { get; set; }
            public decimal EstimatedFine { get; set; }
        }

        public async Task OnGetAsync()
        {
            await LoadOverdueRecords();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadOverdueRecords();
                return Page();
            }

            using var conn = new OracleConnection(_connectionString);
            conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 1. 更新借阅记录状态和实际归还日期
                using var updateCmd = new OracleCommand(
                    @"UPDATE BorrowRecord 
                      SET status = 'overdue_returned',
                          return_date = :returnDate,
                          remarks = :remarks
                      WHERE record_id = :recordId
                        AND status = 'overdue'",
                    conn);
                updateCmd.Transaction = transaction;
                updateCmd.Parameters.Add("returnDate", Input.ActualReturnDate);
                updateCmd.Parameters.Add("remarks", Input.Remarks ?? string.Empty);
                updateCmd.Parameters.Add("recordId", Input.RecordId);

                int rowsUpdated = updateCmd.ExecuteNonQuery();

                if (rowsUpdated == 0)
                {
                    ModelState.AddModelError("", "记录更新失败，可能记录不存在或状态已变更");
                    await LoadOverdueRecords();
                    return Page();
                }

                // 2. 添加罚款记录
                using var getusercmd = new OracleCommand(
                    @"SELECT due_date
                      FROM borrow_record
                      WHERE record_id = :recordId", conn);
                getusercmd.Transaction = transaction;
                getusercmd.Parameters.Add("recordId", Input.RecordId);
                object? result = getusercmd.ExecuteScalar();
                DateTime dueDate = DateTime.Today;
                if (result != null && result!=DBNull.Value)
                {
                    dueDate = Convert.ToDateTime(result);
                }



                using var insertFineCmd = new OracleCommand(
                    @"UPDATE violation_record
                      SET description=:description, points_deducted=:points
                       WHERE record_id = :recordId, AND type = '逾期'",
                    conn);
                int point = 0;
                insertFineCmd.Transaction = transaction;
                insertFineCmd.Parameters.Add("description", $"逾期共{Input.ActualReturnDate-dueDate}天, 罚款{Input.FineAmount}元");
                insertFineCmd.Parameters.Add("points", point);
                insertFineCmd.Parameters.Add("recordId", Input.RecordId);

                insertFineCmd.ExecuteNonQuery();

                transaction.Commit();

                TempData["SuccessMessage"] = $"成功处理逾期记录 #{Input.RecordId}！罚款金额：{Input.FineAmount:C}";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError("", $"处理失败：{ex.Message}");
                await LoadOverdueRecords();
                return Page();
            }
        }

        private async Task LoadOverdueRecords()
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new OracleCommand(
                @"SELECT br.record_id, br.user_id, u.user_name, b.title, b.book_id, 
                         br.borrow_date, br.due_date,
                         TRUNC(SYSDATE) - br.due_date AS overdue_days
                  FROM BorrowRecord br
                  JOIN Users u ON u.user_id = br.user_id
                  JOIN Books b ON b.book_id = br.book_id
                  WHERE br.status = 'overdue'
                  ORDER BY br.due_date ASC",
                conn);

            using var reader = cmd.ExecuteReader();
            OverdueRecords = new List<OverdueRecord>();

            while (reader.Read())
            {
                var dueDate = reader.GetDateTime(6);
                // 使用新的罚款计算器计算罚款
                var estimatedFine = FineCalculator.CalculateOverdueFine(dueDate);
                
                OverdueRecords.Add(new OverdueRecord
                {
                    RecordId = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    UserName = reader.GetString(2),
                    BookTitle = reader.GetString(3),
                    BookId = reader.GetInt32(4),
                    BorrowDate = reader.GetDateTime(5),
                    DueDate = dueDate,
                    OverdueDays = reader.GetInt32(7),
                    EstimatedFine = estimatedFine
                });
            }
        }
    }
}