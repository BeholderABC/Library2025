using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebLibrary.Pages.Borrow
{
    public class DashboardModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public List<BorrowingBook> BorrowingBooks { get; set; } = new();

        public DashboardModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return;

            using var conn = new OracleConnection(_configuration.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = new OracleCommand(
                @"SELECT b.title, br.due_date, br.record_id, br.copy_id
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
                if(daysLeft < 0)
                {
                    status = "逾期";
                }
                else if (daysLeft == 0)
                {
                    status = "今天到期";
                }
                BorrowingBooks.Add(new BorrowingBook
                {
                    BookTitle = reader["title"].ToString(),
                    DueDate = dueDate,
                    Status = status,
                    RecordId = Convert.ToInt32(reader["record_id"]),
                    CopyId = Convert.ToInt32(reader["copy_id"])
                });
            }
        }

        public IActionResult OnPostReturnBook()
        {
            TempData["Message"] = "请联系图书管理员处理还书事项";
            return RedirectToPage();
        }

        public class BorrowingBook
        {
            public string BookTitle { get; set; }
            public DateTime DueDate { get; set; }
            public string Status { get; set; }
            public int RecordId { get; set; }
            public int CopyId { get; set; }
        }
    }
}
