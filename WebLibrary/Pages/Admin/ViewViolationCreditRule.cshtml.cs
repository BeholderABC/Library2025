using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Admin
{
    public class ViewViolationCreditRuleModel : PageModel
    {
        public int FirstPeriodDays { get; private set; }
        public int FirstPeriodPenalty { get; private set; }
        public int ExtraDailyPenalty { get; private set; }
        public int OverdueFreezeThreshold { get; private set; }
        public int DamagePenalty { get; private set; }
        public int OnTimeBonus { get; private set; }

        private readonly IConfiguration _config;
        public ViewViolationCreditRuleModel(IConfiguration config) => _config = config;

        public void OnGet()
        {
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT FIRST_PERIOD_DAYS,
       FIRST_PERIOD_PENALTY,
       EXTRA_DAILY_PENALTY,
       OVERDUE_FREEZE_THRESHOLD,
       DAMAGE_PENALTY,
       ONTIME_BONUS
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
    }
}
