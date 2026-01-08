using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System;

namespace WebLibrary.Pages.Admin
{
    public class ViolationCreditRuleManagementModel : PageModel
    {
        // 绑定的表单字段
        [BindProperty] public int FirstPeriodDays { get; set; }
        [BindProperty] public int FirstPeriodPenalty { get; set; }
        [BindProperty] public int ExtraDailyPenalty { get; set; }
        [BindProperty] public int OverdueFreezeThreshold { get; set; }
        [BindProperty] public int DamagePenalty { get; set; }
        [BindProperty] public int OnTimeBonus { get; set; }

        private readonly IConfiguration _config;
        public ViolationCreditRuleManagementModel(IConfiguration config)
            => _config = config;

        public void OnGet()
        {
            // 加载数据库中的第一条规则
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT FIRST_PERIOD_DAYS, FIRST_PERIOD_PENALTY, EXTRA_DAILY_PENALTY,
       OVERDUE_FREEZE_THRESHOLD, DAMAGE_PENALTY, ONTIME_BONUS
  FROM VIOLATION_CREDIT_RULE
 WHERE RULE_ID = (SELECT MIN(RULE_ID) FROM VIOLATION_CREDIT_RULE)";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                FirstPeriodDays = reader.GetInt32(0);
                FirstPeriodPenalty = reader.GetInt32(1);
                ExtraDailyPenalty = reader.GetInt32(2);
                OverdueFreezeThreshold = reader.GetInt32(3);
                DamagePenalty = reader.GetInt32(4);
                OnTimeBonus = reader.GetInt32(5);
            }
        }

        public IActionResult OnPost()
        {
            // 保存或更新到数据库（UPSERT）
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
MERGE INTO VIOLATION_CREDIT_RULE tgt
USING (SELECT 1 AS KEYCOL FROM dual) src
   ON (tgt.RULE_ID = (SELECT MIN(RULE_ID) FROM VIOLATION_CREDIT_RULE))
WHEN MATCHED THEN
  UPDATE SET 
    FIRST_PERIOD_DAYS        = :fpd,
    FIRST_PERIOD_PENALTY     = :fpp,
    EXTRA_DAILY_PENALTY      = :edp,
    OVERDUE_FREEZE_THRESHOLD = :oft,
    DAMAGE_PENALTY           = :dp,
    ONTIME_BONUS             = :otb
WHEN NOT MATCHED THEN
  INSERT (
    FIRST_PERIOD_DAYS, FIRST_PERIOD_PENALTY, EXTRA_DAILY_PENALTY,
    OVERDUE_FREEZE_THRESHOLD, DAMAGE_PENALTY, ONTIME_BONUS
  ) VALUES (
    :fpd, :fpp, :edp, :oft, :dp, :otb
  )";
            cmd.BindByName = true;
            cmd.Parameters.Add("fpd", FirstPeriodDays);
            cmd.Parameters.Add("fpp", FirstPeriodPenalty);
            cmd.Parameters.Add("edp", ExtraDailyPenalty);
            cmd.Parameters.Add("oft", OverdueFreezeThreshold);
            cmd.Parameters.Add("dp", DamagePenalty);
            cmd.Parameters.Add("otb", OnTimeBonus);
            cmd.ExecuteNonQuery();

            TempData["Message"] = "违规信用分规则已保存！";
            return RedirectToPage();
        }
    }
}

