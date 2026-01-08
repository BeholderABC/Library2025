using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Shared.Utils
{
    public class ViolationCreditRule
    {
        public int FirstPeriodDays { get; set; }
        public int FirstPeriodPenalty { get; set; }
        public int ExtraDailyPenalty { get; set; }
        public int OverdueFreezeThreshold { get; set; }
        public int DamagePenalty { get; set; }
        public int OnTimeBonus { get; set; }
    }

    public class ViolationCreditRuleService
    {

        private readonly string _connStr;

        public ViolationCreditRuleService(IConfiguration config)
        {
            _connStr = config.GetConnectionString("OracleDb");
        }

        public ViolationCreditRule GetCurrentRule()
        {
            using var conn = new OracleConnection(_connStr);
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
                return new ViolationCreditRule
                {
                    FirstPeriodDays = reader.GetInt32(0),
                    FirstPeriodPenalty = reader.GetInt32(1),
                    ExtraDailyPenalty = reader.GetInt32(2),
                    OverdueFreezeThreshold = reader.GetInt32(3),
                    DamagePenalty = reader.GetInt32(4),
                    OnTimeBonus = reader.GetInt32(5)
                };
            }
            throw new Exception("未找到规则");
        }
    }
}
