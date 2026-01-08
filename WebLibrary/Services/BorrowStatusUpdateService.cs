using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Security.Claims; 
using System.Threading;
using System.Threading.Tasks;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Services
{
    public class BorrowStatusUpdateService : BackgroundService
    {
        private readonly ILogger<BorrowStatusUpdateService> _logger;
        private readonly string _connectionString;
        //private readonly ViolationCreditRuleService _ruleService;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // 每天检查一次

        public BorrowStatusUpdateService(IConfiguration config, ILogger<BorrowStatusUpdateService> logger)
        {
            _connectionString = config.GetConnectionString("OracleDb");
            _logger = logger;
            //_ruleService = ruleService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("借阅状态更新服务已启动");

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateBorrowStatus();
                    _logger.LogInformation("借阅状态更新完成");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "借阅状态更新失败");
                }
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        /*
        private async Task<int> CountPastViolations(OracleConnection conn, OracleTransaction tx, int userId)
        {
            using var cmd = new OracleCommand(
                "SELECT COUNT(*) FROM violation_record WHERE user_id = :uid AND type = 'overdue'", conn)
            { Transaction = tx };
            cmd.Parameters.Add("uid", userId);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        private async Task<int> GetCurrentCredit(OracleConnection conn, OracleTransaction tx, int userId)
        {
            using var cmd = new OracleCommand(
                "SELECT CREDIT_SCORE FROM USERS WHERE USER_ID = :uid", conn)
            { Transaction = tx };
            cmd.Parameters.Add("uid", userId);
            var obj = await cmd.ExecuteScalarAsync();
            return obj == null ? 0 : Convert.ToInt32(obj);
        }
        */

        public class CreditCalculator
        {
            public static (int Delta, int NewScore) ComputeCreditChange(
     int currentCredit,
     DateTime dueDate,
     int firstPeriodDays,
     int firstPeriodPenalty,
     int extraDailyPenalty)
            {
                int overdueDays = (DateTime.Today - dueDate.Date).Days;

                if (overdueDays <= 0)
                    return (0, currentCredit); // 未逾期

                int delta = overdueDays <= firstPeriodDays
                    ? -firstPeriodPenalty
                    : -firstPeriodPenalty - (overdueDays - firstPeriodDays) * extraDailyPenalty;

                int newScore = Math.Max(0, currentCredit + delta);
                return (delta, newScore);
            }
        }

        private async Task UpdateBorrowStatus() 
        {
            using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();
            using var transaction = conn.BeginTransaction();
            try
            {
                // 更新逾期状态
                using var updateCmd = new OracleCommand(
                    @"UPDATE BorrowRecord 
                SET status = 'overdue'
                WHERE status = 'lending' 
                    AND due_date < SYSDATE", conn);
                updateCmd.Transaction = transaction;
                int updatedCount = await updateCmd.ExecuteNonQueryAsync();

                if (updatedCount > 0)
                {
                    _logger.LogInformation($"已更新 {updatedCount} 条记录为逾期状态");
                }

                using var cmdFineOverdues = new OracleCommand(@"
                    SELECT record_id, user_id, due_date
                    FROM BorrowRecord
                    WHERE status = 'overdue'
                      AND NOT EXISTS (
                          SELECT 1 FROM violation_record
                          WHERE record_id = BorrowRecord.record_id AND type = 'overdue'
                      )", conn);

                cmdFineOverdues.Transaction = transaction;
                using var readerOverdues = await cmdFineOverdues.ExecuteReaderAsync();
                while (await readerOverdues.ReadAsync())
                {
                    int recordId = readerOverdues.GetInt32(0);
                    int userId = readerOverdues.GetInt32(1);
                    DateTime dueDate = readerOverdues.GetDateTime(2);

                    using var cmdInsertViolation = new OracleCommand(@"
                        INSERT INTO violation_record(recorded_by, recorded_at, user_id, record_id, type, occurred_at)
                        VALUES(:adminID, SYSDATE, :userID, :recordID, '逾期', :occurredAt)", conn);

                    cmdInsertViolation.Transaction = transaction;
                    cmdInsertViolation.Parameters.Add("adminID", -1);
                    cmdInsertViolation.Parameters.Add("userID", userId);
                    cmdInsertViolation.Parameters.Add("recordID", recordId);
                    cmdInsertViolation.Parameters.Add("occurredAt", dueDate);

                    await cmdInsertViolation.ExecuteNonQueryAsync();
                    _logger.LogInformation($"记录了用户{userId}的逾期不还违规行为，相关借阅记录ID为{recordId}");


                    // 当前日期（只比较日期，忽略时间）
                    DateTime today = DateTime.Today;
                    int daysOverdue = (today - dueDate.Date).Days;

                    // 2. 读取当前信用分

                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "SELECT CREDIT_SCORE FROM USERS WHERE USER_ID = :userID";
                    cmd.Parameters.Add("userID", userId);

                    Console.WriteLine($"SQL: {cmd.CommandText}");
                    Console.WriteLine($"Parameters: uid = {userId}");

                    var result = await cmd.ExecuteScalarAsync();
                    int currScore =  (result != null && result != DBNull.Value ? Convert.ToInt32(result) : 60);

                    // 3. 计算扣分（核心逻辑）
                    int delta = 0;

                    if (daysOverdue <= 5)
                    {
                        delta = 10; // 5天内扣10分
                    }
                    else
                    {
                        delta = 10 + (daysOverdue - 5) * 3; // 超过5天：10 + 每天3分
                    }
                    int newScore = Math.Max(0, currScore - delta); // 信用分不低于0

                    // 4. 更新用户信用分
                    using var cmdUpd = new OracleCommand(
                        "UPDATE USERS SET CREDIT_SCORE = :p_cs WHERE USER_ID = :p_uid", conn);
                    cmdUpd.Transaction = transaction;
                    cmdUpd.Parameters.Add("p_cs", newScore);
                    cmdUpd.Parameters.Add("p_uid", userId);
                    Console.WriteLine($"SQL: {cmdUpd.CommandText}");
                    Console.WriteLine($"Parameters: cs {newScore}, uid {userId}");

                    await cmdUpd.ExecuteNonQueryAsync();

                    _logger.LogInformation($"用户{userId}信用分由{currScore}变为{newScore}，本次扣分：{delta}分（逾期{daysOverdue}天）");

                    //更新信用分


                    /*
                    // —— 新增扣分逻辑 ——
                    // 读取当前规则
                    var rule = _ruleService.GetCurrentRule();
                    // 计算逾期天数和历史逾期次数
                    //int daysOverdue = (DateTime.Today - dueDate.Date).Days;
                    int pastCount = await CountPastViolations(conn, transaction, userId);

                    // 取当前信用分
                    int currScore = await GetCurrentCredit(conn, transaction, userId);

                    // 调用计算器
                    
                    var (delta, newScore) = CreditCalculator.ComputeCreditChange(
                        currentCredit: currScore,
                        dueDate: dueDate.Date,                 
                        rule: rule);
                    
                    
                    // 更新用户信用分
                    using var cmdUpd = new OracleCommand(
                        "UPDATE USERS SET CREDIT_SCORE = :cs WHERE USER_ID = :uid", conn)
                    { Transaction = transaction };
                    cmdUpd.Parameters.Add("cs", newScore);
                    cmdUpd.Parameters.Add("uid", userId);
                    await cmdUpd.ExecuteNonQueryAsync();

                    _logger.LogInformation($"用户{userId}信用分由{currScore}变为{newScore}，delta={delta}");
                    */

                    /*
                    // —— 新增：直接读取规则，不使用注入服务 —— //
                    int firstPeriodDays = 7, firstPeriodPenalty = 5, extraDailyPenalty = 1;

                    using (var cmdRule = conn.CreateCommand())
                    {
                        cmdRule.Transaction = transaction;
                        cmdRule.CommandText = @"
        SELECT FIRST_PERIOD_DAYS, FIRST_PERIOD_PENALTY, EXTRA_DAILY_PENALTY
        FROM VIOLATION_CREDIT_RULE
        WHERE ROWNUM = 1";
                        using var readerRule = await cmdRule.ExecuteReaderAsync();
                        if (await readerRule.ReadAsync())
                        {
                            firstPeriodDays = readerRule.GetInt32(0);
                            firstPeriodPenalty = readerRule.GetInt32(1);
                            extraDailyPenalty = readerRule.GetInt32(2);
                        }
                    }

                    //  正确获取信用分
                    string? creditStr = User.FindFirst("CreditScore")?.Value;

                    int currScore = string.IsNullOrEmpty(creditStr) ? 60 : int.Parse(creditStr);


                    var (delta, newScore) = CreditCalculator.ComputeCreditChange(
       currentCredit: currScore,
       dueDate: dueDate,
       firstPeriodDays: firstPeriodDays,
       firstPeriodPenalty: firstPeriodPenalty,
       extraDailyPenalty: extraDailyPenalty);


                    */
                }
                // 更新逾期归还状态（如果实际归还日期在逾期后）
                using var updateReturnedCmd = new OracleCommand(
                    @"UPDATE BorrowRecord 
                SET status = 'overdue_returned'
                WHERE status = 'overdue' 
                    AND return_date IS NOT NULL", conn);
                updateReturnedCmd.Transaction = transaction;

                int returnedUpdated = await updateReturnedCmd.ExecuteNonQueryAsync();
                _logger.LogInformation($"更新了 {returnedUpdated} 条逾期记录为逾期归还状态");

                using var updateNormalCmd = new OracleCommand(
                    @"UPDATE BorrowRecord
                   SET status = 'returned'
                   WHERE status = 'lending'
                     AND return_date IS NOT NULL", conn);
                updateNormalCmd.Transaction = transaction;
                int normalUpdated = await updateNormalCmd.ExecuteNonQueryAsync();
                _logger.LogInformation($"更新了{normalUpdated}条记录为正常归还状态");
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            using var transactionLimit = conn.BeginTransaction();
            try
            {
                using var cmdToLimit = new OracleCommand(
                    @"SELECT br.user_id, count(*)
                  FROM BorrowRecord br
                  JOIN Users u ON br.user_id = u.user_id
                  WHERE br.status IN ('overdue', 'overdue_returned')
                    AND br.due_date >= SYSDATE - 90
                    AND (u.is_limited = 0 OR u.limit_end_date < SYSDATE)
                  GROUP BY br.user_id
                  HAVING COUNT(*) > 2",
                    conn);

                cmdToLimit.Transaction = transactionLimit;
                using var reader = cmdToLimit.ExecuteReader();
                var usersToLimit = new List<int>();
                while (reader.Read())
                {
                    int userId = reader.GetInt32(0);
                    int overdueCount = reader.GetInt32(1);
                    usersToLimit.Add(userId);
                    _logger.LogInformation($"用户{userId}在三个月内逾期{overdueCount}次，将被限制借阅权限");
                }

                foreach (int userId in usersToLimit)
                {
                    using var cmdLimit = new OracleCommand(
                        @"UPDATE Users
                      SET is_limited = 1,
                          limit_end_date = SYSDATE + 30
                      WHERE user_id = :userId
                        AND (is_limited = 0 OR limit_end_date < SYSDATE)",
                        conn);
                    cmdLimit.Parameters.Add("userId", userId);
                    cmdLimit.Transaction = transactionLimit;
                    int rows = await cmdToLimit.ExecuteNonQueryAsync();
                    if (rows > 0)
                    {
                        _logger.LogInformation($"已限制用户{userId}的借阅权限");
                    }
                    else
                    {
                        _logger.LogWarning($"用户{userId}限制状态未更新，可能已被手动处理");
                    }
                }
                using var cmdRelease = new OracleCommand(
                    @"UPDATE Users
                  SET is_limited = 0,
                      limit_end_date = NULL
                  WHERE is_limited = 1
                    AND (limit_end_date IS NULL OR limit_end_date <= SYSDATE)"
                    , conn);
                cmdRelease.Transaction = transactionLimit;
                int releasedCount = await cmdRelease.ExecuteNonQueryAsync();
                if (releasedCount > 0)
                {
                    _logger.LogInformation($"已自动解除{releasedCount}个用户的借阅限制");
                }
                transactionLimit.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理用户限制时出错");
                transactionLimit.Rollback();
            }
        }
    }
}
