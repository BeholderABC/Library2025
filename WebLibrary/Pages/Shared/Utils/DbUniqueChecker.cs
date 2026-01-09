using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;   // 为了读取连接串

namespace WebLibrary.Pages.Shared.Utils
{
    public static class DbUniqueChecker
    {
        
        public static bool Exists(IConfiguration cfg,
                                  string column,
                                  string? value,
                                  int? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;     // 空值视为不重复

            using var conn = new OracleConnection(
                cfg.GetConnectionString("OracleDb"));
            conn.Open();

            string sql = excludeId.HasValue
                ? $"SELECT 1 FROM USERS WHERE {column} = :val AND USER_ID <> :id"
                : $"SELECT 1 FROM USERS WHERE {column} = :val";

            using var cmd = new OracleCommand(sql, conn);
            cmd.BindByName = true;
            cmd.Parameters.Add("val", value);
            if (excludeId.HasValue)
                cmd.Parameters.Add("id", excludeId.Value);

            return cmd.ExecuteScalar() != null;
        }
    }
}