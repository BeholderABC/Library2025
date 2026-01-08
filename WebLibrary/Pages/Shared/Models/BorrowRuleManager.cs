namespace WebLibrary.Pages.Shared.Models
{

   
        // Models/BorrowRuleManager.cs

        // 单个角色的借阅规则模型
        public class RoleBorrowRule
        {
            public string Role { get; set; } = string.Empty;
            public int BaseBorrowDays { get; set; } // 基础借阅天数
            public int MinDays { get; set; } // 最小天数限制
            public int MaxDays { get; set; } // 最大天数限制
            public int MaxBooks { get; set; } // 最大可借数量
            public int HighCreditThreshold { get; set; } // 高信用分阈值（多借）
            public int LowCreditThreshold { get; set; } // 低信用分阈值（少借）
            public int ExtraDays { get; set; } = 14; // 信用分额外天数（固定14天）
            public int ExtraBooks { get; set; } = 1; // 信用分额外本数（固定1本）
        }

        // 静态规则管理器（全局唯一）
        public static class BorrowRuleManager
        {
            // 初始化各角色默认规则
            public static RoleBorrowRule StudentRule { get; set; } = new()
            {
                Role = "学生",
                BaseBorrowDays = 30,
                MinDays = 15,
                MaxDays = 60,
                MaxBooks = 3,
                HighCreditThreshold = 80,
                LowCreditThreshold = 30
            };

            public static RoleBorrowRule AdminRule { get; set; } = new()
            {
                Role = "图书馆管理员",
                BaseBorrowDays = 60,
                MinDays = 30,
                MaxDays = 90,
                MaxBooks = 7,
                HighCreditThreshold = 80,
                LowCreditThreshold = 30
            };

            public static RoleBorrowRule StaffRule { get; set; } = new()
            {
                Role = "其他教职工",
                BaseBorrowDays = 45,
                MinDays = 15,
                MaxDays = 90,
                MaxBooks = 5,
                HighCreditThreshold = 80,
                LowCreditThreshold = 30
            };

            // 更新规则（供管理员修改）
            public static void UpdateRule(RoleBorrowRule updatedRule)
            {
                Console.WriteLine($"开始更新规则：{updatedRule.Role}，基础天数：{updatedRule.BaseBorrowDays}");
                switch (updatedRule.Role)
                {
                        case "学生":
                            StudentRule = updatedRule;
                            break;
                        case "图书馆管理员":
                            AdminRule = updatedRule;
                            break;
                        case "其他教职工":
                            StaffRule = updatedRule;
                            break;
                }
            }
        }

}


