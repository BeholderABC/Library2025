using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace WebLibrary.Pages.Borrow
{
    public class ReservationModel : PageModel
    {
        public List<ReservationItem> Reservations { get; set; } = new List<ReservationItem>();
        private readonly IConfiguration _configuration;

        public ReservationModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return;
            string connStr = _configuration.GetConnectionString("OracleDb");
            using (var conn = new OracleConnection(connStr))
            {
                conn.Open();
                using (var cmd = new OracleCommand(@"SELECT r.reservation_id, r.user_id, r.book_id, r.reservation_date, r.status, r.expiry_date, r.queue_position, b.title 
                FROM Reservation r JOIN Book b ON r.book_id = b.book_id 
                WHERE r.user_id = :userId 
                ORDER BY CASE WHEN r.status = 'pending' THEN 0 ELSE 1 END, r.reservation_date DESC", conn))
                {
                    cmd.Parameters.Add("userId", int.Parse(userId));
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            Reservations.Add(new ReservationItem
                            {
                                ReservationId = reader["reservation_id"].ToString(),
                                UserId = reader["user_id"].ToString(),
                                BookId = reader["book_id"].ToString(),
                                BookTitle = reader["title"].ToString(),
                                QueuePosition = reader["queue_position"] == DBNull.Value ? "" : reader["queue_position"].ToString(),
                                Status = reader["status"].ToString(),
                                ReservationDate = reader["reservation_date"].ToString()
                            });
                        }
                    }
                }
                // 1. 查询 fulfilled 且当天有借阅记录的预约
                using (var cmd = new OracleCommand(@"
                    SELECT r.reservation_id, b.title
                    FROM Reservation r
                    JOIN Book b ON r.book_id = b.book_id
                    JOIN Copy c ON c.book_id = r.book_id
                    JOIN BorrowRecord br ON br.user_id = r.user_id AND br.copy_id = c.copy_id
                    WHERE r.user_id = :userId
                      AND r.status = 'fulfilled'
                      AND TRUNC(br.borrow_date) = TRUNC(SYSDATE)", conn))
                {
                    cmd.Parameters.Add("userId", int.Parse(userId));
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            TempData["FulfilledMessage"] = $"您已成功借阅{reader["title"]}";
                            // 2. 弹窗后立即将该预约状态设为 notified
                            var reservationId = reader["reservation_id"];
                            using (var cmdUpdate = new OracleCommand(
                                "UPDATE Reservation SET status = 'notified' WHERE reservation_id = :rid", conn))
                            {
                                cmdUpdate.Parameters.Add("rid", reservationId);
                                cmdUpdate.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
        }

        public IActionResult OnPostCancel(string id)
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return RedirectToPage();
            string connStr = _configuration.GetConnectionString("OracleDb");
            using (var conn = new OracleConnection(connStr))
            {
                conn.Open();
                // 1. 查出被取消预约的book_id和queue_position
                string bookId = null;
                int? queuePos = null;
                using (var cmd = new OracleCommand("SELECT book_id, queue_position FROM Reservation WHERE reservation_id = :id AND user_id = :userId", conn))
                {
                    cmd.Parameters.Add("id", int.Parse(id));
                    cmd.Parameters.Add("userId", int.Parse(userId));
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            bookId = reader["book_id"].ToString();
                            queuePos = reader["queue_position"] == DBNull.Value ? null : Convert.ToInt32(reader["queue_position"]);
                        }
                    }
                }
                // 2. 先取消本预约
                using (var cmd = new OracleCommand("UPDATE Reservation SET status = 'cancelled', queue_position = NULL WHERE reservation_id = :id AND user_id = :userId", conn))
                {
                    cmd.Parameters.Add("id", int.Parse(id));
                    cmd.Parameters.Add("userId", int.Parse(userId));
                    cmd.ExecuteNonQuery();
                }
                // 3. 更新后面所有人的队列位次
                if (bookId != null && queuePos != null)
                {
                    using (var cmd = new OracleCommand("UPDATE Reservation SET queue_position = queue_position - 1 WHERE book_id = :bookId AND queue_position > :queuePos AND status IN ('pending', 'notified')", conn))
                    {
                        cmd.Parameters.Add("bookId", bookId);
                        cmd.Parameters.Add("queuePos", queuePos);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return RedirectToPage();
        }

        public class ReservationItem
        {
            public string ReservationId { get; set; }
            public string UserId { get; set; }
            public string BookId { get; set; }
            public string BookTitle { get; set; }
            public string QueuePosition { get; set; }
            public string Status { get; set; }
            public string ReservationDate { get; set; }
        }
    }
}
