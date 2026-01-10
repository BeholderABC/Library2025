using System;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Shared.Utils
{
    public class NotificationService
    {
        private readonly string _connStr;

        public NotificationService(IConfiguration config)
        {
            _connStr = config.GetConnectionString("OracleDb");
        }

        /// <summary>
        /// 发送给指定用户（需要提供中文角色和用户ID）
        /// </summary>
        public void SendToUser(string roleCn, string userId, string content, string senderId, string receiverName)
        {
            string table = roleCn switch
            {
                "学生" => "NOTIFICATIONS_STUDENT",
                "图书馆管理员" => "NOTIFICATIONS_LIBRARIAN",
                "其他教职工" => "NOTIFICATIONS_STAFF",
                _ => throw new ArgumentException("未知角色", nameof(roleCn))
            };
            
            using var conn = new OracleConnection(_connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();

            //Console.WriteLine($"INSERT INTO {table} (USER_ID, CONTENT, IS_READ, CREATE_TIME, SENDER_ID,USER_NAME) VALUES (:userid, :msgcontent, 0, SYSDATE, :senderid, :username)");

            cmd.CommandText = $@"
INSERT INTO {table} (USER_ID, CONTENT, IS_READ, CREATE_TIME, SENDER_ID,USER_NAME)
VALUES (:userid, :msgcontent, 0, SYSDATE, :senderid, :username)";

            cmd.Parameters.Add("userid", userId);
            cmd.Parameters.Add("msgcontent", content);
            cmd.Parameters.Add("senderid", senderId);
            cmd.Parameters.Add("username", receiverName);
            cmd.ExecuteNonQuery();

        }

        /// <summary>
        /// 发送通知给某类角色所有用户
        /// </summary>
        public void SendToRole(string roleCn, string content, string senderId, string receiverName)
        {
            string table = roleCn switch
            {
                "学生" => "NOTIFICATIONS_STUDENT",
                "图书馆管理员" => "NOTIFICATIONS_LIBRARIAN",
                "其他教职工" => "NOTIFICATIONS_STAFF",
                _ => throw new ArgumentException("未知角色", nameof(roleCn))
            };

            using var conn = new OracleConnection(_connStr);
            conn.Open();

            using var fetchCmd = conn.CreateCommand();
            fetchCmd.CommandText = "SELECT USER_ID FROM USERS WHERE USER_TYPE = :roleCn";
            fetchCmd.Parameters.Add("roleCn", roleCn);
            using var reader = fetchCmd.ExecuteReader();

            while (reader.Read())
            {
                string uid = reader.GetString(0);
                using var insertCmd = conn.CreateCommand();
                insertCmd.CommandText = $@"
INSERT INTO {table} (ID, USER_ID, CONTENT, IS_READ, CREATE_TIME, SENDER_ID,USER_NAME)
VALUES (NOTIFICATION_SEQ.NEXTVAL,:r_uid, :r_content, 0, SYSDATE, :senderid,:username)";
                insertCmd.Parameters.Add("r_uid", uid);
                insertCmd.Parameters.Add("r_content", content);
                insertCmd.Parameters.Add("senderid", senderId);
                insertCmd.Parameters.Add("username", receiverName);
                insertCmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 发送通知给所有角色
        /// </summary>
        public void SendToAll(string content, string senderId, string receiverName)
        {
            SendToRole("学生", content, senderId,receiverName);
            SendToRole("图书馆管理员", content, senderId, receiverName);
            SendToRole("其他教职工", content, senderId, receiverName);
        }
    }
}
