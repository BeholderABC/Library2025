using System;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;


namespace WebLibrary.Pages.Shared.Utils
{
    public class NotificationStatusService
    {
        private readonly IConfiguration _config;

        public NotificationStatusService(IConfiguration config)
        {
            _config = config;
        }

        public int GetUnreadCount(string userId, string roleCn)
        {
            string table = roleCn switch
            {
                "学生" => "NOTIFICATIONS_STUDENT",
                "图书馆管理员" => "NOTIFICATIONS_LIBRARIAN",
                "其他教职工" => "NOTIFICATIONS_STAFF",
                _ => throw new ArgumentException("未知角色", nameof(roleCn))
            };

            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE USER_ID = :uid AND IS_READ = 0";
            cmd.Parameters.Add("uid", userId);
            var count = Convert.ToInt32(cmd.ExecuteScalar());

            Console.WriteLine(count);

            return count;
        }
    }
}
