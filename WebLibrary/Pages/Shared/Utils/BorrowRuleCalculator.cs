using System;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Shared.Utils
{
    public static class BorrowRuleCalculator
    {
        /// <summary>
        /// 计算用户最终可借天数。
        /// </summary>
        /// <param name="role">角色名称：Student、Librarian、Staff</param>
        /// <param name="requestedDays">用户选择/输入的借期</param>
        /// <param name="creditScore">用户当前信用分</param>
        /// <param name="config">注入的 IConfiguration，用于获取连接串</param>
        /// <returns>最终可借天数</returns>
        public static int CalculateBorrowDays(
            string role,
            
            int creditScore,
            IConfiguration config)
        {
            // 默认值以防查询不到
            int minDays = 3, maxDays = 5;
            int bonusThreshold = 0, bonusDays = 0;
            int penaltyThreshold = 0, penaltyDays = 0;
            int requestedDays = 3;

            string connStr = config.GetConnectionString("OracleDb");
            using var conn = new OracleConnection(connStr);
            conn.Open();

            // 1. 读取角色借期上下限
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT MIN_DAYS, MAX_DAYS,ORIGIN_DAY
  FROM BORROW_RULES
 WHERE ROLE_NAME = :role";
                cmd.Parameters.Add("role", role);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    minDays = reader.GetInt32(0);
                    maxDays = reader.GetInt32(1);
                    requestedDays = reader.GetInt32(2);
                }
            }

            Console.WriteLine($"role:{role},minDays:{minDays},maxDays:{maxDays},requestedDays:{requestedDays}");


            // 2. 读取加分规则（取第一条）
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT BONUS_THRESHOLD, BONUS_DAYS,PENALTY_THRESHOLD,PENALTY_DAYS
  FROM CREDIT_RULES
 WHERE RULE_ID = (SELECT MIN(RULE_ID) FROM CREDIT_RULES)";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    bonusThreshold = reader.GetInt32(0);
                    bonusDays = reader.GetInt32(1);
                    penaltyThreshold = reader.GetInt32(2);
                    penaltyDays = reader.GetInt32(3);

                }
            }
            int days=requestedDays;
            // 3. 如果信用分足够，则加上 bonusDays
            if (creditScore >= bonusThreshold)
                days += bonusDays;
            if (creditScore <= penaltyThreshold)
                days -= penaltyDays;

            // 4. clamp 用户请求的天数到[minDays, maxDays]
             days = Math.Min(Math.Max(requestedDays, minDays), maxDays);

           



            return days;
        }
    }

}

/*使用方法

// 假设你在页面模型中已经通过 User.Claims 拿到用户的角色和信用分
var userRole = User.FindFirst("Role")?.Value ?? "Student";
var userCredit = int.Parse(User.FindFirst("CreditScore")?.Value ?? "0");

// 如果你页面上还让用户输入/选择了一个“期望借期”（requestedDays），就从表单里取：
// （否则可以把 requestedDays 也定死为某个默认值）
int requestedDays = int.Parse(Request.Form["RequestedDays"]);

// 调用我们的 BorrowRuleCalculator 来算出最终可借天数
int borrowDays = BorrowRuleCalculator.CalculateBorrowDays(
    userRole,
    requestedDays,
    userCredit,
    _config);

// 最终的到期日就用这个天数
var dueDate = DateTime.Now.AddDays(borrowDays);

 */

