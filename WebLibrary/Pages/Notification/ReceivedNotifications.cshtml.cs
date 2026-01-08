using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Security.Claims;
using System;
using System.Collections.Generic;

namespace WebLibrary.Pages.Notification
{
    public class ReceivedNotificationsModel : PageModel
    {
        public class NotificationItem
        {
            public long Id { get; set; }
            public DateTime CreateTime { get; set; }
            public string SenderId { get; set; } = "";
            public string ContentPreview { get; set; } = "";
            public bool IsRead { get; set; }
        }

        public List<NotificationItem> Notifications { get; private set; } = new();

        private readonly IConfiguration _config;
        public ReceivedNotificationsModel(IConfiguration config) => _config = config;

        public void OnGet()
        {
            var userId = User.FindFirst("UserId")?.Value;
            var roleCn = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userId == null || roleCn == null) return;

            // 选择表
            string table = roleCn switch
            {
                "学生" => "NOTIFICATIONS_STUDENT",
                "图书馆管理员" => "NOTIFICATIONS_LIBRARIAN",
                "其他教职工" => "NOTIFICATIONS_STAFF",
                _ => throw new ArgumentException("未知角色")
            };

            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
SELECT ID, SENDER_ID, CONTENT, IS_READ, CREATE_TIME
  FROM {table}
 WHERE USER_ID = :userId
 ORDER BY CREATE_TIME DESC";
            cmd.Parameters.Add("userId", userId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var full = reader.GetString(2);
                Notifications.Add(new NotificationItem
                {
                    Id = reader.GetInt64(0),
                    SenderId = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ContentPreview = full.Length <= 50 ? full : full.Substring(0, 50) + "...",
                    IsRead = reader.GetInt32(3) == 1,
                    CreateTime = reader.GetDateTime(4)
                });
            }
        }
    }
}
