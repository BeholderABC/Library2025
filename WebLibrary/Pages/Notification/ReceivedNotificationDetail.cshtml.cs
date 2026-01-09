using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;
using System;


namespace WebLibrary.Pages.Notification
{
    public class ReceivedNotificationDetailModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public long Id { get; set; }

        public DateTime CreateTime { get; private set; }
        public string SenderId { get; private set; } = "";
        public new string Content { get; private set; } = "";

        private readonly IConfiguration _config;
        public ReceivedNotificationDetailModel(IConfiguration config) => _config = config;

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            var roleCn = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userId == null || roleCn == null) return;

            string table = roleCn switch
            {
                "学生" => "NOTIFICATIONS_STUDENT",
                "图书馆管理员" => "NOTIFICATIONS_LIBRARIAN",
                "教师" => "NOTIFICATIONS_STAFF",
                _ => throw new ArgumentException("无效的参数")
            };

            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
SELECT SENDER_ID, CONTENT, IS_READ, CREATE_TIME
  FROM {table}
 WHERE ID = :Id AND USER_ID = :userId";
            cmd.Parameters.Add("Id", Id);
            cmd.Parameters.Add("userId", userId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                SenderId = reader.IsDBNull(0) ? "" : reader.GetString(0);
                Content = reader.GetString(1);
                CreateTime = reader.GetDateTime(3);

                // ���Ϊ�Ѷ�
                if (reader.GetInt32(2) == 0)
                {
                    MarkAsRead(conn, table, Id);
                }
            }
        }

        private void MarkAsRead(OracleConnection conn, string table, long id)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
UPDATE {table}
   SET IS_READ = 1
 WHERE ID = :id";
            cmd.Parameters.Add("id", id);
            cmd.ExecuteNonQuery();
        }
    }
}

