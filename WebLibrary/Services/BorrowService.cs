using Oracle.ManagedDataAccess.Client;



namespace WebLibrary.Services
{
    public class BorrowService
    {
        private readonly string _connectionString;

        public BorrowService(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("OracleDb");
        }

        public DateTime CalculateDueDate(string userType, DateTime borrowDate, int renew_times)
        {
            int Base_borrow_time;
            if (userType == "student")
            {
                Base_borrow_time = 30;
            }
            else if (userType == "teacher")
            {
                Base_borrow_time = 60;
            }
            else
            {
                Base_borrow_time = 30;
            }
            return borrowDate.AddDays(Base_borrow_time + (renew_times * 15));
        }
        public class RenewResult
        {
            public bool Success { get; }
            public string Message { get; }
            public DateTime? NewDueDate { get; }
            public RenewResult(bool success, string message, DateTime? newDueDate)
            {
                Success = success;
                Message = message;
                NewDueDate = newDueDate;
            }
        }
            // 续借图书
        public RenewResult RenewBorrowRecord(int recordId, int userId)
        {
            using var conn = new OracleConnection(_connectionString);
            conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 设置命令的事务
                using var getCmd = new OracleCommand(
                    @"SELECT due_date, renew_times, status
                      FROM BorrowRecord
                      WHERE record_id = :id
                        AND user_id = :uid", conn);
                getCmd.Transaction = transaction;
                getCmd.Parameters.Add("id", recordId);
                getCmd.Parameters.Add("uid", userId);

                DateTime dueDate;
                int renewTimes;
                String status;

                using (var reader = getCmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        transaction.Rollback();
                        return new RenewResult(false, "借阅记录不存在", null);
                    }

                    dueDate = reader.GetDateTime(0);
                    renewTimes = reader.GetInt32(1);
                    status = reader.GetString(2);
                }

                var validationError = ValidateRenewalConditions(dueDate, renewTimes, status);
                if (validationError != null)
                {
                    transaction.Rollback();
                    return new RenewResult(false, validationError, null);
                }

                var newDueDate = dueDate.AddDays(15);
                using var updateCmd = new OracleCommand(
                    @"UPDATE BorrowRecord 
                  SET due_date = :newDue, 
                      renew_times = renew_times + 1 
                  WHERE record_id = :id", conn);
                updateCmd.Transaction = transaction;

                updateCmd.Parameters.Add("newDue", newDueDate);
                updateCmd.Parameters.Add("id", recordId);
                var rowsAffected = updateCmd.ExecuteNonQuery();

                if (rowsAffected == 0)
                {
                    transaction.Rollback();
                    return new RenewResult(false, "更新借阅记录失败", null);
                }

                transaction.Commit();
                return new RenewResult(true, $"续借成功！新应还日期：{newDueDate:yyyy-MM-dd}", newDueDate);
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return new RenewResult(false, $"续借失败：{ex.Message}", null);
            }
        }

        private string ValidateRenewalConditions(DateTime dueDate, int renewTimes, string status)
        {
            var today = DateTime.Today;

            // 检查状态
            if (status != "lending" && status != "overdue")
            {
                return "只有未归还的图书才能续借";
            }

            // 检查续借次数
            if (renewTimes >= 2)
            {
                return "已达到最大续借次数（2次）";
            }

            // 检查续借时间
            var earliestRenewDate = dueDate.AddDays(-10);
            if (today < earliestRenewDate)
            {
                return $"只能在应还日期前10天内续借（最早 {earliestRenewDate:yyyy-MM-dd}）";
            }

            // 逾期后特殊处理
            if (status == "overdue" && today > dueDate)
            {
                return "图书已逾期，请尽快归还并缴纳罚款";
            }

            return null;
        }
    }
}
