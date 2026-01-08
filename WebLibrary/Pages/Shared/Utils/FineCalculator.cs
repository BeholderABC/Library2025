using System;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;

namespace WebLibrary.Pages.Shared.Utils
{
    /// <summary>
    /// 根据罚款规则计算逾期罚款金额
    /// </summary>
    public static class FineCalculator
    {
        /// <summary>
        /// 计算逾期罚款
        /// 根据FineRules.cshtml中的规则：1-7天每日1元，8-30天每日2元
        /// </summary>
        /// <param name="dueDate">应还日期</param>
        /// <param name="actualDate">实际日期（默认为今天）</param>
        /// <returns>应缴纳的罚款金额</returns>
        public static decimal CalculateOverdueFine(DateTime dueDate, DateTime? actualDate = null)
        {
            var checkDate = actualDate ?? DateTime.Today;
            var overdueDays = (checkDate - dueDate.Date).Days;
            
            if (overdueDays <= 0)
                return 0; // 未逾期
            
            decimal fine = 0;
            
            // 1-7天：每日1元
            if (overdueDays <= 7)
            {
                fine = overdueDays * 1;
            }
            else
            {
                // 前7天按1元/天计算
                fine = 7 * 1;
                // 8-30天：每日2元
                var additionalDays = Math.Min(overdueDays - 7, 23); // 最多23天（8-30天）
                fine += additionalDays * 2;
                
                // 超过30天的部分继续按2元/天计算
                if (overdueDays > 30)
                {
                    fine += (overdueDays - 30) * 2;
                }
            }
            
            return fine;
        }
        
        /// <summary>
        /// 计算从最后罚款日期到现在的增量罚款
        /// LAST_FINED_DATE 表示截止到该日期为止的罚款已经全部缴清
        /// </summary>
        /// <param name="dueDate">应还日期</param>
        /// <param name="lastFinedDate">最后罚款日期 - 截止到该日期的罚款已缴清</param>
        /// <param name="currentDate">当前日期（默认为今天）</param>
        /// <returns>需要补缴的罚款金额</returns>
        public static decimal CalculateIncrementalFine(DateTime dueDate, DateTime lastFinedDate, DateTime? currentDate = null)
        {
            var checkDate = currentDate ?? DateTime.Today;
            
            // 调试信息
            System.Diagnostics.Debug.WriteLine($"=== CalculateIncrementalFine 详细计算 ===");
            System.Diagnostics.Debug.WriteLine($"DueDate: {dueDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"LastFinedDate: {lastFinedDate:yyyy-MM-dd HH:mm:ss}");
            System.Diagnostics.Debug.WriteLine($"CheckDate: {checkDate:yyyy-MM-dd HH:mm:ss}");
            
            // 检查日期是否只有日期部分，没有时间部分
            System.Diagnostics.Debug.WriteLine($"DueDate.Date: {dueDate.Date:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"LastFinedDate.Date: {lastFinedDate.Date:yyyy-MM-dd}");
            System.Diagnostics.Debug.WriteLine($"CheckDate.Date: {checkDate.Date:yyyy-MM-dd}");
            
            // 如果还没到检查日期，没有新罚款
            if (checkDate.Date <= lastFinedDate.Date)
            {
                System.Diagnostics.Debug.WriteLine("No new fine - checkDate.Date <= lastFinedDate.Date");
                return 0;
            }
            
            // 计算从lastFinedDate的下一天到checkDate的罚款
            var startBillingFrom = lastFinedDate.Date.AddDays(1);
            System.Diagnostics.Debug.WriteLine($"StartBillingFrom (initial): {startBillingFrom:yyyy-MM-dd}");
            
            // 如果开始计费日期还没到期，从到期日开始计费
            if (startBillingFrom <= dueDate.Date)
            {
                startBillingFrom = dueDate.Date.AddDays(1);
                System.Diagnostics.Debug.WriteLine($"StartBillingFrom (adjusted): {startBillingFrom:yyyy-MM-dd}");
            }
            
            // 如果开始计费日期已经超过检查日期，没有新罚款
            if (startBillingFrom > checkDate.Date)
            {
                System.Diagnostics.Debug.WriteLine("No new fine - startBillingFrom > checkDate.Date");
                return 0;
            }
            
            // 计算从startBillingFrom到checkDate的天数罚款
            decimal incrementalFine = 0;
            var currentBillingDate = startBillingFrom;
            
            while (currentBillingDate <= checkDate.Date)
            {
                // 计算当前日期是逾期第几天
                var daysSinceOverdue = (currentBillingDate - dueDate.Date).Days;
                
                System.Diagnostics.Debug.WriteLine($"Billing date: {currentBillingDate:yyyy-MM-dd}, Days since overdue: {daysSinceOverdue}");
                
                if (daysSinceOverdue <= 7)
                {
                    incrementalFine += 1; // 1-7天：每日1元
                    System.Diagnostics.Debug.WriteLine($"Added 1 yuan (day {daysSinceOverdue})");
                }
                else
                {
                    incrementalFine += 2; // 8天以上：每日2元
                    System.Diagnostics.Debug.WriteLine($"Added 2 yuan (day {daysSinceOverdue})");
                }
                
                currentBillingDate = currentBillingDate.AddDays(1);
            }
            
            System.Diagnostics.Debug.WriteLine($"Total incremental fine: {incrementalFine}");
            System.Diagnostics.Debug.WriteLine("=== 计算结束 ===");
            return incrementalFine;
        }
    }
}
