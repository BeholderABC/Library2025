using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using WebLibrary.Pages.Shared.Models;

namespace WebLibrary.Pages.Borrow
{
    public class HistoryModel : PageModel
    {
        private readonly IConfiguration _configuration;
        public List<BorrowHistoryRecord> BorrowHistory { get; set; } = new();

        public HistoryModel(IConfiguration configuration)
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
                @"SELECT br.record_id, b.title, b.author, br.borrow_date, br.due_date, br.status, br.renew_times, br.return_date
                  FROM BorrowRecord br
                  JOIN Copy c ON br.copy_id = c.copy_id
                  JOIN Book b ON c.book_id = b.book_id
                  WHERE br.user_id = :userId
                  ORDER BY br.borrow_date DESC", conn);
            cmd.Parameters.Add("userId", int.Parse(userId));
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                BorrowHistory.Add(new BorrowHistoryRecord
                {
                    RecordId = Convert.ToInt32(reader["record_id"]),
                    BookTitle = reader["title"].ToString(),
                    Author = reader["author"].ToString(),
                    BorrowDate = Convert.ToDateTime(reader["borrow_date"]),
                    DueDate = Convert.ToDateTime(reader["due_date"]),
                    Status = reader["status"].ToString(),
                    RenewTimes = Convert.ToInt32(reader["renew_times"]),
                    ReturnDate = reader["return_date"] != DBNull.Value ? Convert.ToDateTime(reader["return_date"]) : null
                });
            }
        }
    }

    public class BorrowHistoryRecord
    {
        public int RecordId { get; set; }
        public string BookTitle { get; set; }
        public string Author { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; }
        public int RenewTimes { get; set; }
        public DateTime? ReturnDate { get; set; }
    }
} 