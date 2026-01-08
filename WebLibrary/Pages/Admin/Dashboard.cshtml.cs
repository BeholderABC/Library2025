using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;

namespace WebLibrary.Pages.Admin
{
    
    public class DashboardModel : PageModel
    {
        private readonly IConfiguration _config;

        // 定义统计数据属性，供前端绑定
        public int UserCount { get; set; }       // 用户总数
        public int BookCount { get; set; }       // 藏书总量（不同书籍数量）
        public int TotalCopies { get; set; }     // 图书总副本数（可选，根据需求展示）
        public int LendingCount { get; set; }    // 在借数
        public int OverdueCount { get; set; }    // 逾期数

        // 构造函数注入配置
        public DashboardModel(IConfiguration config)
        {
            _config = config;
        }

        // GET请求：加载统计数据
        public void OnGet()
        {
            // 连接数据库
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            // 1. 查询用户总数（user表所有记录数）
            UserCount = GetStatistic(conn, "SELECT COUNT(user_id) FROM users");

            // 2. 查询藏书量（book表中不同书籍的数量）
            BookCount = GetStatistic(conn, "SELECT COUNT(book_id) FROM book");

            // （可选）查询总副本数（所有书籍的total_copies总和）
            TotalCopies = GetStatistic(conn, "SELECT SUM(total_copies) FROM book");

            // 3. 查询在借数（状态为'lending'的借阅记录）
            LendingCount = GetStatistic(conn, "SELECT COUNT(record_id) FROM BorrowRecord WHERE status = 'lending'");

            // 4. 查询逾期数（状态为'overdue'的借阅记录）
            OverdueCount = GetStatistic(conn, "SELECT COUNT(record_id) FROM BorrowRecord WHERE status = 'overdue'");
        }

        // 辅助方法：执行统计查询并返回结果
        private int GetStatistic(OracleConnection conn, string sql)
        {
            using var cmd = new OracleCommand(sql, conn);
            // 打印 SQL 语句（调试用）
            Console.WriteLine("执行的 SQL：" + sql);
            try
            {
                var result = cmd.ExecuteScalar(); // 报错位置
                return result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
            catch (OracleException ex)
            {
                // 打印错误信息和 SQL（关键！）
                Console.WriteLine($"错误信息：{ex.Message}，执行的 SQL：{sql}");
                throw; // 继续抛出异常，不掩盖问题
            }
        }
    }
}