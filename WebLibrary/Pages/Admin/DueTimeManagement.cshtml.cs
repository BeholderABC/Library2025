
// Pages/Admin/BorrowRuleManagement.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;

namespace WebLibrary.Pages.Admin
{
    public class BorrowRuleManagementModel : PageModel
    {
        public List<string> Roles { get; } = new() { "学生", "图书馆管理员", "其他教职工" };
        // 本地缓存，从数据库或表单读来的值
        public Dictionary<string, (int MinDays, int MaxDays, int MaxBooks,int OriginDays)> Rules { get; private set; } = new();

        [BindProperty] public int CreditBonusThreshold { get; set; }
        [BindProperty] public int CreditBonusDays { get; set; }
        [BindProperty] public int CreditBonusBooks { get; set; }
        [BindProperty] public int CreditPenaltyThreshold { get; set; }
        [BindProperty] public int CreditPenaltyBooks { get; set; }

        [BindProperty] public int CreditPenaltyDays { get; set; }


        private readonly IConfiguration _config;
        public BorrowRuleManagementModel(IConfiguration config) => _config = config;

        /// <summary>
        /// 页面首次加载：从数据库加载已有规则
        /// </summary>
        public void OnGet()
        {
            // 初始化字典
            foreach (var role in Roles)
                Rules[role] = (1, 1, 1,1);

            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            // 1. 读取角色借阅规则
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT ROLE_NAME, MIN_DAYS, MAX_DAYS, MAX_BOOKS,ORIGIN_DAY
  FROM BORROW_RULES";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var role = reader.GetString(0);
                    var min = reader.GetInt32(1);
                    var max = reader.GetInt32(2);
                    var mb = reader.GetInt32(3);
                    var od = reader.GetInt32(4);
                    if (Rules.ContainsKey(role))
                        Rules[role] = (min, max, mb,od);
                }
            }

            // 2. 读取信用分规则（只取第一条）
            using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = @"
SELECT BONUS_THRESHOLD, BONUS_DAYS, BONUS_BOOKS, PENALTY_THRESHOLD, PENALTY_BOOKS, PENALTY_DAYS
  FROM CREDIT_RULES
 WHERE RULE_ID = (SELECT MIN(RULE_ID) FROM CREDIT_RULES)";
                using var r2 = cmd2.ExecuteReader();
                if (r2.Read())
                {
                    CreditBonusThreshold = r2.GetInt32(0);
                    CreditBonusDays = r2.GetInt32(1);
                    CreditBonusBooks = r2.GetInt32(2);
                    CreditPenaltyThreshold = r2.GetInt32(3);
                    CreditPenaltyBooks = r2.GetInt32(4);
                    CreditPenaltyDays = r2.GetInt32(5);
                }
            }
        }

        /// <summary>
        /// 提交表单时：将规则写回数据库（使用 MERGE 实现 UPSERT）
        /// </summary>
        public IActionResult OnPost()
        {
            // 从表单中读取到 Rules 已由 Razor 自动绑定到 BindProperty 以外字段
            foreach (var role in Roles)
            {
                var minDays = int.Parse(Request.Form[$"{role}-MinDays"]);
                var maxDays = int.Parse(Request.Form[$"{role}-MaxDays"]);
                var maxBooks = int.Parse(Request.Form[$"{role}-MaxBooks"]);
                var originDays = int.Parse(Request.Form[$"{role}-OriginDays"]);
                Rules[role] = (minDays, maxDays, maxBooks,originDays);
            }

            CreditBonusThreshold = int.Parse(Request.Form["CreditBonusThreshold"]);
            CreditBonusDays = int.Parse(Request.Form["CreditBonusDays"]);
            CreditBonusBooks = int.Parse(Request.Form["CreditBonusBooks"]);
            CreditPenaltyThreshold = int.Parse(Request.Form["CreditPenaltyThreshold"]);
            CreditPenaltyBooks = int.Parse(Request.Form["CreditPenaltyBooks"]);
            CreditPenaltyDays = int.Parse(Request.Form["CreditPenaltyDays"]);

            // 写入数据库
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            // 1. 更新 BORROW_RULES 表
            foreach (var role in Roles)
            {
                var (minD, maxD, mb,od) = Rules[role];
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
MERGE INTO BORROW_RULES tgt
USING (SELECT :role AS ROLE_NAME FROM dual) src
   ON (tgt.ROLE_NAME = src.ROLE_NAME)
WHEN MATCHED THEN
  UPDATE SET MIN_DAYS = :minD, MAX_DAYS = :maxD, MAX_BOOKS = :mb,ORIGIN_DAY=:od
WHEN NOT MATCHED THEN
  INSERT (ROLE_NAME, MIN_DAYS, MAX_DAYS, MAX_BOOKS,ORIGIN_DAY)
  VALUES (:role, :minD, :maxD, :mb,:od)";
                cmd.Parameters.Add("role", role);
                cmd.Parameters.Add("minD", minD);
                cmd.Parameters.Add("maxD", maxD);
                cmd.Parameters.Add("mb", mb);
                cmd.Parameters.Add("od", od);
                cmd.ExecuteNonQuery();
            }

            // 2. 更新 CREDIT_RULES 表（假设只有一条记录）
            using (var cmd2 = conn.CreateCommand())
            {
                cmd2.CommandText = @"
MERGE INTO CREDIT_RULES tgt
USING (SELECT 1 AS KEYCOL FROM dual) src
   ON (tgt.RULE_ID = (SELECT MIN(RULE_ID) FROM CREDIT_RULES))
WHEN MATCHED THEN
  UPDATE SET 
    BONUS_THRESHOLD   = :bth,
    BONUS_DAYS        = :bdy,
    BONUS_BOOKS       = :bbk,
    PENALTY_THRESHOLD = :pth,
    PENALTY_BOOKS     = :pbk,
    PENALTY_DAYS      = :pdy
WHEN NOT MATCHED THEN
  INSERT (BONUS_THRESHOLD, BONUS_DAYS, BONUS_BOOKS, PENALTY_THRESHOLD, PENALTY_BOOKS, PENALTY_DAYS)
  VALUES (:bth, :bdy, :bbk, :pth, :pbk, :pdy)";
                cmd2.Parameters.Add("bth", CreditBonusThreshold);
                cmd2.Parameters.Add("bdy", CreditBonusDays);
                cmd2.Parameters.Add("bbk", CreditBonusBooks);
                cmd2.Parameters.Add("pth", CreditPenaltyThreshold);
                cmd2.Parameters.Add("pbk", CreditPenaltyBooks);
                cmd2.Parameters.Add("pdy", CreditPenaltyDays);
                cmd2.ExecuteNonQuery();
            }

            TempData["Message"] = "借阅规则已成功保存至数据库！";
            // 重定向回 GET，避免刷新重复提交
            return RedirectToPage();
        }

        // 辅助方法用于页面渲染
        public int GetMinDays(string role) => Rules.ContainsKey(role) ? Rules[role].MinDays : 1;
        public int GetMaxDays(string role) => Rules.ContainsKey(role) ? Rules[role].MaxDays : 1;
        public int GetMaxBooks(string role) => Rules.ContainsKey(role) ? Rules[role].MaxBooks : 1;
        public int GetOriginDays(string role) =>Rules.ContainsKey(role) ? Rules[role].OriginDays : 1;
    }
}
