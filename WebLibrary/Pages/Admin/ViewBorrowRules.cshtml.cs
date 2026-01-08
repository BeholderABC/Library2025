
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;


namespace WebLibrary.Pages.Admin
{
    public class ViewBorrowRulesModel : PageModel
    {

        public class RoleRule
        {
            public string RoleName { get; set; } = "";
            public int MinDays { get; set; }
            public int MaxDays { get; set; }
            public int MaxBooks { get; set; }

            public int OriginDays { get; set; }
        }

        public class CreditRuleModel
        {
            public int BonusThreshold { get; set; }
            public int BonusDays { get; set; }
            public int BonusBooks { get; set; }
            public int PenaltyThreshold { get; set; }
            public int PenaltyBooks { get; set; }

            public int PenaltyDays { get; set; } 
        }

        public List<RoleRule> RoleRules { get; private set; } = new();
        public CreditRuleModel CreditRule { get; private set; } = new();

        private readonly IConfiguration _config;
        public ViewBorrowRulesModel(IConfiguration config) => _config = config;

        public void OnGet()
        {
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            // 1. 查询 BORROW_RULES
            using (var cmd = new OracleCommand("SELECT ROLE_NAME, MIN_DAYS, MAX_DAYS, MAX_BOOKS, ORIGIN_DAY FROM BORROW_RULES", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    RoleRules.Add(new RoleRule
                    {
                        RoleName = reader.GetString(0),
                        MinDays = reader.GetInt32(1),
                        MaxDays = reader.GetInt32(2),
                        MaxBooks = reader.GetInt32(3),
                        OriginDays = reader.GetInt32(4)
                    });
                }
            }

            // 2. 查询 CREDIT_RULES（只取一条）
            using (var cmd = new OracleCommand(@"
SELECT BONUS_THRESHOLD, BONUS_DAYS, BONUS_BOOKS, PENALTY_THRESHOLD, PENALTY_BOOKS,PENALTY_DAYS
FROM CREDIT_RULES 
WHERE RULE_ID = (SELECT MIN(RULE_ID) FROM CREDIT_RULES)", conn))
            using (var reader = cmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    CreditRule = new CreditRuleModel
                    {
                        BonusThreshold = reader.GetInt32(0),
                        BonusDays = reader.GetInt32(1),
                        BonusBooks = reader.GetInt32(2),
                        PenaltyThreshold = reader.GetInt32(3),
                        PenaltyBooks = reader.GetInt32(4),
                        PenaltyDays = reader.GetInt32(5)
                    };
                }
            }

        }
    }
}
