using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace WebLibrary.Pages.Borrow
{
    [Route("api/CheckReservationNotify")]
    [ApiController]
    public class CheckReservationNotifyController : ControllerBase
    {
        private readonly IConfiguration _c;
        public CheckReservationNotifyController(IConfiguration c) { _c = c; }

        [HttpGet]
        public IActionResult Get()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (userId == null) return Ok();
            using var conn = new OracleConnection(_c.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = new OracleCommand(@"
                SELECT reservation_id, book_id FROM Reservation
                WHERE user_id = :userId AND status = 'fulfilled'
                FETCH FIRST 1 ROWS ONLY", conn);
            cmd.Parameters.Add("userId", int.Parse(userId));
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var reservationId = reader["reservation_id"];
                var bookId = reader["book_id"];
                // 查书名
                string bookTitle = "";
                using (var cmd2 = new OracleCommand("SELECT title FROM Book WHERE book_id = :bid", conn))
                {
                    cmd2.Parameters.Add("bid", bookId);
                    using var r2 = cmd2.ExecuteReader();
                    if (r2.Read()) bookTitle = r2["title"].ToString();
                }
                // 标记已通知
                using (var cmd3 = new OracleCommand("UPDATE Reservation SET status = 'notified' WHERE reservation_id = :rid", conn))
                {
                    cmd3.Parameters.Add("rid", reservationId);
                    cmd3.ExecuteNonQuery();
                }
                return Ok(new { message = $"您已成功借阅{bookTitle}" });
            }
            return Ok();
        }
    }
} 