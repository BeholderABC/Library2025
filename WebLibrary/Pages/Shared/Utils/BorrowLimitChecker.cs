using System;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Shared.Utils
{
    /// <summary>
    /// 检查借阅限制和计算可借书本数
    /// </summary>
    public static class BorrowLimitChecker
    {
        /// <summary>
        /// 检查用户是否可以借书，并返回可借本数
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="userRole">用户角色</param>
        /// <param name="userCreditScore">用户信用分</param>
        /// <param name="config">配置对象</param>
        /// <returns>(canBorrow: 是否可以借书, maxBooks: 最大可借本数, currentBorrows: 当前借书数量, message: 提示信息)</returns>
        public static (bool canBorrow, int maxBooks, int currentBorrows, string message) CheckBorrowingEligibility(
            int userId, 
            string userRole, 
            int userCreditScore, 
            IConfiguration config)
        {
            string connStr = config.GetConnectionString("OracleDb");
            using var conn = new OracleConnection(connStr);
            conn.Open();

            // 1. 检查用户是否被限制借阅
            using (var cmd = new OracleCommand("SELECT is_limited FROM Users WHERE user_id = :userId", conn))
            {
                cmd.Parameters.Add("userId", userId);
                var isLimited = cmd.ExecuteScalar();
                if (isLimited != null && Convert.ToInt32(isLimited) == 1)
                {
                    return (false, 0, 0, "您的账户当前处于限制状态，无法借阅。");
                }
            }

            // 2. 获取当前借阅数量
            int currentBorrows = 0;
            using (var cmd = new OracleCommand(
                "SELECT COUNT(*) FROM BorrowRecord WHERE user_id = :userId AND status IN ('lending', 'overdue', 'fined')", 
                conn))
            {
                cmd.Parameters.Add("userId", userId);
                currentBorrows = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // 3. 检查是否有逾期未处理的图书
            using (var cmd = new OracleCommand(
                "SELECT COUNT(*) FROM BorrowRecord WHERE user_id = :userId AND status = 'overdue'", 
                conn))
            {
                cmd.Parameters.Add("userId", userId);
                var overdueCount = Convert.ToInt32(cmd.ExecuteScalar());
                if (overdueCount > 0)
                {
                    return (false, 0, currentBorrows, $"您有{overdueCount}本图书逾期未还，请先处理逾期图书。");
                }
            }

            // 4. 获取用户角色的基础借阅规则
            int baseMaxBooks = 3; // 默认值
            

            // 5. 根据信用分调整最大借书数量
            int bonusThreshold = 80, bonusBooks = 0;
            int penaltyThreshold = 40, penaltyBooks = 0;
            
            

            int adjustedMaxBooks = baseMaxBooks;
            
            // 信用分加成
            if (userCreditScore >= bonusThreshold)
            {
                adjustedMaxBooks += bonusBooks;
            }
            
            // 信用分惩罚
            if (userCreditScore < penaltyThreshold)
            {
                adjustedMaxBooks = Math.Max(1, adjustedMaxBooks - penaltyBooks); // 至少保证可以借1本
            }

            // 6. 检查是否已达到借阅上限
            if (currentBorrows >= adjustedMaxBooks)
            {
                return (false, adjustedMaxBooks, currentBorrows, 
                    $"您已达到最大借阅数量({adjustedMaxBooks}本)，请先归还部分图书。");
            }

            return (true, adjustedMaxBooks, currentBorrows, 
                "");
        }

        /// <summary>
        /// 获取用户的最大可借本数（不检查当前借阅状态）
        /// </summary>
        public static int GetMaxBorrowableBooks(string userRole, int userCreditScore, IConfiguration config)
        {
            string connStr = config.GetConnectionString("OracleDb");
            using var conn = new OracleConnection(connStr);
            conn.Open();

            // 获取基础最大借书数
            int baseMaxBooks = 3;
            using (var cmd = new OracleCommand("SELECT MAX_BOOKS FROM BORROW_RULES WHERE ROLE_NAME = :role", conn))
            {
                cmd.Parameters.Add("role", userRole);
                var result = cmd.ExecuteScalar();
                if (result != null)
                {
                    baseMaxBooks = Convert.ToInt32(result);
                }
            }

            // 获取信用分规则
            int bonusThreshold = 80, bonusBooks = 0;
            int penaltyThreshold = 40, penaltyBooks = 0;
            
            using (var cmd = new OracleCommand(@"
                SELECT BONUS_THRESHOLD, BONUS_BOOKS, PENALTY_THRESHOLD, PENALTY_BOOKS 
                FROM CREDIT_RULES 
                WHERE RULE_ID = (SELECT MIN(RULE_ID) FROM CREDIT_RULES)", conn))
            {
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    bonusThreshold = reader.GetInt32(0);
                    bonusBooks = reader.GetInt32(1);
                    penaltyThreshold = reader.GetInt32(2);
                    penaltyBooks = reader.GetInt32(3);
                }
            }

            int adjustedMaxBooks = baseMaxBooks;
            
            if (userCreditScore >= bonusThreshold)
            {
                adjustedMaxBooks += bonusBooks;
            }
            
            if (userCreditScore < penaltyThreshold)
            {
                adjustedMaxBooks = Math.Max(1, adjustedMaxBooks - penaltyBooks);
            }

            return adjustedMaxBooks;
        }
    }
}
