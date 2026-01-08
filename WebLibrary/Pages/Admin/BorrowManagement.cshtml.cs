using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.InkML;
using System.Data;
using System.Text;


//namespace WebLibrary.Pages.Admin
//{
//    public class BorrowManagementModel : PageModel
//    {
//        private readonly IConfiguration _cfg;
//        public BorrowManagementModel(IConfiguration cfg) => _cfg = cfg;

//        public List<BorrowRecord> BorrowRecords { get; set; } = new();

//        public List<SelectListItem> UserOptions { get; set; } = new();
//        public List<SelectListItem> BookOptions { get; set; } = new();

//        [BindProperty]
//        public NewBorrowInputModel NewBorrow { get; set; } = new();

//        // 筛选参数
//        [BindProperty(SupportsGet = true)]
//        public DateTime? StartDate { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public DateTime? EndDate { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public int? BookIdMin { get; set; }

//        [BindProperty(SupportsGet = true)]
//        public int? BookIdMax { get; set; }

//        public class NewBorrowInputModel
//        {
//            [Required] public int? UserId { get; set; }
//            [Required] public int? BookId { get; set; }
//            [Required] public int? CopyId { get; set; }
//            [Required] public DateTime BorrowDate { get; set; } = DateTime.Today;
//            [Required] public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);
//        }

//        public void OnGet()
//        {
//            LoadData();

//        }
//        public IActionResult OnPostCreate()
//        {
//            LoadData(); // 必须先加载下拉列表和已有数据，以免出错

//            if (!ModelState.IsValid)
//                return Page();

//            try
//            {
//                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
//                conn.Open();

//                // 生成新的 RECORD_ID（测试用，无序列）
//                int newId;
//                using (var cmd1 = new OracleCommand("SELECT NVL(MAX(RECORD_ID), 0) + 1 FROM ADMINISTRATOR.BORROWRECORD", conn))
//                {
//                    newId = Convert.ToInt32(cmd1.ExecuteScalar());
//                }

//                using var cmd = conn.CreateCommand();
//                cmd.CommandText = @"
//            INSERT INTO ADMINISTRATOR.BORROWRECORD 
//            (RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, STATUS, RENEW_TIMES, RETURN_DATE, LAST_FINED_DATE)
//            VALUES (:record_id, :user_id, :copy_id, :borrow_date, :due_date, 'overdue', 0, NULL, NULL)";

//                cmd.Parameters.Add("record_id", newId);
//                cmd.Parameters.Add("user_id", NewBorrow.UserId);
//                cmd.Parameters.Add("copy_id", NewBorrow.CopyId);
//                cmd.Parameters.Add("borrow_date", NewBorrow.BorrowDate);
//                cmd.Parameters.Add("due_date", NewBorrow.DueDate);
//                //cmd.Parameters.Add("status", NewBorrow.DueDate);

//                cmd.ExecuteNonQuery();

//                return RedirectToPage(); // 成功后刷新页面
//            }
//            catch (OracleException ex)
//            {
//                // 外键错误（Copy ID 不存在）
//                if (ex.Message.Contains("FK_BORROWRECORD_COPY"))
//                {
//                    ModelState.AddModelError(string.Empty, "指定的图书副本不存在（Copy ID）。请检查输入。");
//                }
//                // 用户不存在
//                else if (ex.Message.Contains("FK_BORROWRECORD_USER"))
//                {
//                    ModelState.AddModelError(string.Empty, "指定的用户不存在。请检查 User ID。");
//                }
//                // 其他 Oracle 错误
//                else
//                {
//                    ModelState.AddModelError(string.Empty, $"数据库错误：{ex.Message}");
//                }

//                return Page(); // 返回页面，显示错误信息
//            }
//            catch (Exception ex)
//            {
//                // 非数据库类错误
//                ModelState.AddModelError(string.Empty, $"未知错误：{ex.Message}");
//                return Page();
//            }
//        }

//        public IActionResult OnPostDelete(int id)
//        {
//            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
//            conn.Open();

//            using var cmd = conn.CreateCommand();
//            cmd.CommandText = "DELETE FROM ADMINISTRATOR.BORROWRECORD WHERE RECORD_ID = :id";
//            cmd.Parameters.Add("id", id);
//            cmd.ExecuteNonQuery();

//            return RedirectToPage();
//        }

//        private void LoadData()
//        {
//            BorrowRecords.Clear();
//            BookOptions.Clear();
//            UserOptions.Clear();

//            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
//            conn.Open();

//            // 借阅记录
//            using (var cmd = conn.CreateCommand())
//            {
//                cmd.CommandText = @"SELECT RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, STATUS, RENEW_TIMES 
//                                    FROM ADMINISTRATOR.BORROWRECORD ORDER BY BORROW_DATE DESC";
//                using var rdr = cmd.ExecuteReader();
//                while (rdr.Read())
//                {
//                    BorrowRecords.Add(new BorrowRecord
//                    {
//                        RecordId = rdr.GetInt32(0),
//                        UserId = rdr.GetInt32(1),
//                        CopyId = rdr.GetInt32(2),
//                        BorrowDate = rdr.GetDateTime(3),
//                        DueDate = rdr.GetDateTime(4),
//                        Status = rdr.GetString(5),
//                        RenewTimes = rdr.GetInt32(6)
//                    });
//                }
//            }

//            // 用户下拉（简化）
//            using (var cmd = conn.CreateCommand())
//            {
//                cmd.CommandText = "SELECT USER_ID, USER_NAME FROM ADMINISTRATOR.USERS ORDER BY USER_NAME";
//                using var rdr = cmd.ExecuteReader();
//                while (rdr.Read())
//                {
//                    UserOptions.Add(new SelectListItem
//                    {
//                        Value = rdr.GetInt32(0).ToString(),
//                        Text = rdr.GetString(1)
//                    });
//                }
//            }

//            // 图书下拉（简化）
//            using (var cmd = conn.CreateCommand())
//            {
//                cmd.CommandText = "SELECT BOOK_ID, TITLE FROM ADMINISTRATOR.BOOK ORDER BY TITLE";
//                using var rdr = cmd.ExecuteReader();
//                while (rdr.Read())
//                {
//                    BookOptions.Add(new SelectListItem
//                    {
//                        Value = rdr.GetInt32(0).ToString(),
//                        Text = rdr.GetString(1)
//                    });
//                }
//            }
//        }
//        public DateTime? CalculateDueDate(OracleConnection conn, string role, int? userInputDays, out string? errorMessage)
//        {
//            errorMessage = null;
//            int minDays = 0, maxDays = 0, originDays = 0;

//            // 1. 获取借阅规则
//            using (var cmd = conn.CreateCommand())
//            {
//                cmd.CommandText = @"
//                SELECT MIN_DAYS, MAX_DAYS, ORIGIN_DAY
//                  FROM BORROW_RULES
//                 WHERE ROLE_NAME = :role";
//                cmd.Parameters.Add("role", role);

//                using var reader = cmd.ExecuteReader();
//                if (reader.Read())
//                {
//                    minDays = reader.GetInt32(0);
//                    maxDays = reader.GetInt32(1);
//                    originDays = reader.GetInt32(2);
//                }
//                else
//                {
//                    errorMessage = $"未找到角色 {role} 的借阅规则。";
//                    return null;
//                }
//            }

//            // 2. 决定使用哪一个天数
//            int finalDays;
//            if (userInputDays.HasValue)
//            {
//                if (userInputDays < minDays || userInputDays > maxDays)
//                {
//                    errorMessage = $"借阅天数必须在 {minDays} 到 {maxDays} 天之间。";
//                    return null;
//                }
//                finalDays = userInputDays.Value;
//            }
//            else
//            {
//                finalDays = originDays;
//            }

//            // 3. 返回 DUE_DATE
//            return DateTime.Today.AddDays(finalDays);
//        }
//        public class BorrowRecordExportDto
//        {
//            public string Username { get; set; }
//            public string BookTitle { get; set; }
//            public DateTime BorrowDate { get; set; }
//            public DateTime DueDate { get; set; }
//            public string Email { get; set; } 
//        }
//        public IActionResult OnGetExportOverdue()
//        {
//            var overdueRecords = new List<BorrowRecordExportDto>();

//            using (var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb")))
//            {
//                conn.Open();

//                string sql = @"
//            SELECT u.USER_NAME, b.TITLE, br.BORROW_DATE, br.DUE_DATE, u.EMAIL
//            FROM ADMINISTRATOR.BORROWRECORD br
//            JOIN ADMINISTRATOR.USERS u ON br.USER_ID = u.USER_ID
//            JOIN ADMINISTRATOR.COPY c ON br.COPY_ID = c.COPY_ID
//            JOIN ADMINISTRATOR.BOOK b ON c.BOOK_ID = b.BOOK_ID
//            WHERE br.RETURN_DATE IS NULL AND br.DUE_DATE < SYSDATE OR br.STATUS = 'overdue'";

//                using var cmd = new OracleCommand(sql, conn);
//                using var reader = cmd.ExecuteReader();
//                while (reader.Read())
//                {
//                    overdueRecords.Add(new BorrowRecordExportDto
//                    {
//                        Username = reader.GetString(0),
//                        BookTitle = reader.GetString(1),
//                        BorrowDate = reader.GetDateTime(2),
//                        DueDate = reader.GetDateTime(3),
//                        Email = reader.IsDBNull(4) ? "" : reader.GetString(4),
//                    });
//                }
//            }

//            using var workbook = new XLWorkbook();
//            var worksheet = workbook.Worksheets.Add("逾期未还");

//            worksheet.Cell(1, 1).Value = "用户名";
//            worksheet.Cell(1, 2).Value = "书名";
//            worksheet.Cell(1, 3).Value = "借出日期";
//            worksheet.Cell(1, 4).Value = "应还日期";
//            worksheet.Cell(1, 5).Value = "邮箱地址";

//            for (int i = 0; i < overdueRecords.Count; i++)
//            {
//                var record = overdueRecords[i];
//                worksheet.Cell(i + 2, 1).Value = record.Username;
//                worksheet.Cell(i + 2, 2).Value = record.BookTitle;
//                worksheet.Cell(i + 2, 3).Value = record.BorrowDate.ToString("yyyy-MM-dd");
//                worksheet.Cell(i + 2, 4).Value = record.DueDate.ToString("yyyy-MM-dd");
//            }

//            using var stream = new MemoryStream();
//            workbook.SaveAs(stream);
//            stream.Seek(0, SeekOrigin.Begin);

//            return File(stream.ToArray(),
//                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
//                        "OverdueRecords.xlsx");
//        }


//    }

//}

namespace WebLibrary.Pages.Admin
{
    public class BorrowManagementModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public BorrowManagementModel(IConfiguration cfg) => _cfg = cfg;

        public List<BorrowRecord> BorrowRecords { get; set; } = new();
        public List<BorrowRecordDisplayModel> DisplayRecords { get; set; } = new();

        public List<SelectListItem> UserOptions { get; set; } = new();
        public List<SelectListItem> BookOptions { get; set; } = new();

        [BindProperty]
        public NewBorrowInputModel NewBorrow { get; set; } = new();

        // 筛选参数
        [BindProperty(SupportsGet = true)]
        public DateTime? StartDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? EndDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? BookIdMin { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? BookIdMax { get; set; }

        // 新增：单个Book ID筛选
        [BindProperty(SupportsGet = true)]
        public int? BookId { get; set; }

        // 新增：状态筛选
        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        public class NewBorrowInputModel
        {
            [Required] public int? UserId { get; set; }
            [Required] public int? BookId { get; set; }
            [Required] public int? CopyId { get; set; }
            [Required] public DateTime BorrowDate { get; set; } = DateTime.Today;
            [Required] public DateTime DueDate { get; set; } = DateTime.Today.AddDays(14);
        }

        public class BorrowRecordDisplayModel
        {
            public int RecordId { get; set; }
            public int UserId { get; set; }
            public string UserName { get; set; } = string.Empty;
            public int CopyId { get; set; }
            public int BookId { get; set; }
            public string BookTitle { get; set; } = string.Empty;
            public DateTime BorrowDate { get; set; }
            public DateTime DueDate { get; set; }
            public string Status { get; set; } = string.Empty;
            public int RenewTimes { get; set; }

            public bool IsOverdue => Status == "overdue" || (DueDate < DateTime.Today && Status != "returned" && Status != "overdue_returned");
        }

        public void OnGet()
        {
            LoadData();
        }

        public IActionResult OnPostCreate()
        {
            LoadData(); // 必须先加载下拉列表和已有数据，以免出错

            if (!ModelState.IsValid)
                return Page();

            try
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                conn.Open();

                // 生成新的 RECORD_ID（测试用，无序列）
                int newId;
                using (var cmd1 = new OracleCommand("SELECT NVL(MAX(RECORD_ID), 0) + 1 FROM ADMINISTRATOR.BORROWRECORD", conn))
                {
                    newId = Convert.ToInt32(cmd1.ExecuteScalar());
                }

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
            INSERT INTO ADMINISTRATOR.BORROWRECORD 
            (RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, STATUS, RENEW_TIMES, RETURN_DATE, LAST_FINED_DATE)
            VALUES (:record_id, :user_id, :copy_id, :borrow_date, :due_date, 'overdue', 0, NULL, NULL)";

                cmd.Parameters.Add("record_id", newId);
                cmd.Parameters.Add("user_id", NewBorrow.UserId);
                cmd.Parameters.Add("copy_id", NewBorrow.CopyId);
                cmd.Parameters.Add("borrow_date", NewBorrow.BorrowDate);
                cmd.Parameters.Add("due_date", NewBorrow.DueDate);

                cmd.ExecuteNonQuery();

                return RedirectToPage(); // 成功后刷新页面
            }
            catch (OracleException ex)
            {
                // 外键错误（Copy ID 不存在）
                if (ex.Message.Contains("FK_BORROWRECORD_COPY"))
                {
                    ModelState.AddModelError(string.Empty, "指定的图书副本不存在（Copy ID）。请检查输入。");
                }
                // 用户不存在
                else if (ex.Message.Contains("FK_BORROWRECORD_USER"))
                {
                    ModelState.AddModelError(string.Empty, "指定的用户不存在。请检查 User ID。");
                }
                // 其他 Oracle 错误
                else
                {
                    ModelState.AddModelError(string.Empty, $"数据库错误：{ex.Message}");
                }

                return Page(); // 返回页面，显示错误信息
            }
            catch (Exception ex)
            {
                // 非数据库类错误
                ModelState.AddModelError(string.Empty, $"未知错误：{ex.Message}");
                return Page();
            }
        }

        public IActionResult OnPostDelete(int id)
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ADMINISTRATOR.BORROWRECORD WHERE RECORD_ID = :id";
            cmd.Parameters.Add("id", id);
            cmd.ExecuteNonQuery();

            return RedirectToPage();
        }

        // 清除筛选条件
        public IActionResult OnPostClearFilters()
        {
            return RedirectToPage(new
            {
                StartDate = (DateTime?)null,
                EndDate = (DateTime?)null,
                BookIdMin = (int?)null,
                BookIdMax = (int?)null,
                BookId = (int?)null,
                Status = (string?)null
            });
        }

        private void LoadData()
        {
            BorrowRecords.Clear();
            DisplayRecords.Clear();
            BookOptions.Clear();
            UserOptions.Clear();

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            // 原有的BorrowRecords查询保持不变
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT RECORD_ID, USER_ID, COPY_ID, BORROW_DATE, DUE_DATE, STATUS, RENEW_TIMES 
                                    FROM ADMINISTRATOR.BORROWRECORD ORDER BY BORROW_DATE DESC";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    BorrowRecords.Add(new BorrowRecord
                    {
                        RecordId = rdr.GetInt32(0),
                        UserId = rdr.GetInt32(1),
                        CopyId = rdr.GetInt32(2),
                        BorrowDate = rdr.GetDateTime(3),
                        DueDate = rdr.GetDateTime(4),
                        Status = rdr.GetString(5),
                        RenewTimes = rdr.GetInt32(6)
                    });
                }
            }

            // 新的DisplayRecords查询，带筛选条件
            var sqlBuilder = new StringBuilder(@"
                SELECT br.RECORD_ID, br.USER_ID, br.COPY_ID, br.BORROW_DATE, br.DUE_DATE, br.STATUS, br.RENEW_TIMES,
                       b.BOOK_ID, b.TITLE as BOOK_TITLE, u.USER_NAME
                FROM ADMINISTRATOR.BORROWRECORD br
                LEFT JOIN ADMINISTRATOR.COPY c ON br.COPY_ID = c.COPY_ID
                LEFT JOIN ADMINISTRATOR.BOOK b ON c.BOOK_ID = b.BOOK_ID
                LEFT JOIN ADMINISTRATOR.USERS u ON br.USER_ID = u.USER_ID
                WHERE 1=1");

            var parameters = new List<OracleParameter>();

            // 时间筛选
            if (StartDate.HasValue)
            {
                sqlBuilder.Append(" AND br.BORROW_DATE >= :start_date");
                parameters.Add(new OracleParameter("start_date", StartDate.Value.Date));
            }

            if (EndDate.HasValue)
            {
                sqlBuilder.Append(" AND br.BORROW_DATE <= :end_date");
                parameters.Add(new OracleParameter("end_date", EndDate.Value.Date.AddDays(1).AddSeconds(-1)));
            }

            // Book ID 筛选
            if (BookId.HasValue)
            {
                sqlBuilder.Append(" AND b.BOOK_ID = :book_id");
                parameters.Add(new OracleParameter("book_id", BookId.Value));
            }
            else
            {
                // Book ID 范围筛选（仅在没有指定具体Book ID时使用）
                if (BookIdMin.HasValue)
                {
                    sqlBuilder.Append(" AND b.BOOK_ID >= :book_id_min");
                    parameters.Add(new OracleParameter("book_id_min", BookIdMin.Value));
                }

                if (BookIdMax.HasValue)
                {
                    sqlBuilder.Append(" AND b.BOOK_ID <= :book_id_max");
                    parameters.Add(new OracleParameter("book_id_max", BookIdMax.Value));
                }
            }

            // 状态筛选
            if (!string.IsNullOrEmpty(Status))
            {
                sqlBuilder.Append(" AND br.STATUS = :status");
                parameters.Add(new OracleParameter("status", Status));
            }

            sqlBuilder.Append(" ORDER BY br.BORROW_DATE DESC");

            using (var cmd = new OracleCommand(sqlBuilder.ToString(), conn) {BindByName = true})
            {
                cmd.Parameters.AddRange(parameters.ToArray());

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    DisplayRecords.Add(new BorrowRecordDisplayModel
                    {
                        RecordId = rdr.GetInt32("RECORD_ID"),
                        UserId = rdr.GetInt32("USER_ID"),
                        CopyId = rdr.GetInt32("COPY_ID"),
                        BorrowDate = rdr.GetDateTime("BORROW_DATE"),
                        DueDate = rdr.GetDateTime("DUE_DATE"),
                        Status = rdr.GetString("STATUS"),
                        RenewTimes = rdr.GetInt32("RENEW_TIMES"),
                        BookId = rdr.IsDBNull("BOOK_ID") ? 0 : rdr.GetInt32("BOOK_ID"),
                        BookTitle = rdr.IsDBNull("BOOK_TITLE") ? "未知" : rdr.GetString("BOOK_TITLE"),
                        UserName = rdr.IsDBNull("USER_NAME") ? "未知" : rdr.GetString("USER_NAME")
                    });
                }
            }

            // 用户下拉（简化）
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT USER_ID, USER_NAME FROM ADMINISTRATOR.USERS ORDER BY USER_NAME";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    UserOptions.Add(new SelectListItem
                    {
                        Value = rdr.GetInt32(0).ToString(),
                        Text = rdr.GetString(1)
                    });
                }
            }

            // 图书下拉（简化）
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT BOOK_ID, TITLE FROM ADMINISTRATOR.BOOK ORDER BY TITLE";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    BookOptions.Add(new SelectListItem
                    {
                        Value = rdr.GetInt32(0).ToString(),
                        Text = rdr.GetString(1)
                    });
                }
            }
        }

        public DateTime? CalculateDueDate(OracleConnection conn, string role, int? userInputDays, out string? errorMessage)
        {
            errorMessage = null;
            int minDays = 0, maxDays = 0, originDays = 0;

            // 1. 获取借阅规则
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                SELECT MIN_DAYS, MAX_DAYS, ORIGIN_DAY
                  FROM BORROW_RULES
                 WHERE ROLE_NAME = :role";
                cmd.Parameters.Add("role", role);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    minDays = reader.GetInt32(0);
                    maxDays = reader.GetInt32(1);
                    originDays = reader.GetInt32(2);
                }
                else
                {
                    errorMessage = $"未找到角色 {role} 的借阅规则。";
                    return null;
                }
            }

            // 2. 决定使用哪一个天数
            int finalDays;
            if (userInputDays.HasValue)
            {
                if (userInputDays < minDays || userInputDays > maxDays)
                {
                    errorMessage = $"借阅天数必须在 {minDays} 到 {maxDays} 天之间。";
                    return null;
                }
                finalDays = userInputDays.Value;
            }
            else
            {
                finalDays = originDays;
            }

            // 3. 返回 DUE_DATE
            return DateTime.Today.AddDays(finalDays);
        }

        public class BorrowRecordExportDto
        {
            public string Username { get; set; }
            public string BookTitle { get; set; }
            public DateTime BorrowDate { get; set; }
            public DateTime DueDate { get; set; }
            public string Email { get; set; }
        }

        public IActionResult OnGetExportOverdue()
        {
            var overdueRecords = new List<BorrowRecordExportDto>();

            using (var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb")))
            {
                conn.Open();

                string sql = @"
            SELECT u.USER_NAME, b.TITLE, br.BORROW_DATE, br.DUE_DATE, u.EMAIL
            FROM ADMINISTRATOR.BORROWRECORD br
            JOIN ADMINISTRATOR.USERS u ON br.USER_ID = u.USER_ID
            JOIN ADMINISTRATOR.COPY c ON br.COPY_ID = c.COPY_ID
            JOIN ADMINISTRATOR.BOOK b ON c.BOOK_ID = b.BOOK_ID
            WHERE br.RETURN_DATE IS NULL AND br.DUE_DATE < SYSDATE OR br.STATUS = 'overdue'";

                using var cmd = new OracleCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    overdueRecords.Add(new BorrowRecordExportDto
                    {
                        Username = reader.GetString(0),
                        BookTitle = reader.GetString(1),
                        BorrowDate = reader.GetDateTime(2),
                        DueDate = reader.GetDateTime(3),
                        Email = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    });
                }
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("逾期未还");

            worksheet.Cell(1, 1).Value = "用户名";
            worksheet.Cell(1, 2).Value = "书名";
            worksheet.Cell(1, 3).Value = "借出日期";
            worksheet.Cell(1, 4).Value = "应还日期";
            worksheet.Cell(1, 5).Value = "邮箱地址";

            for (int i = 0; i < overdueRecords.Count; i++)
            {
                var record = overdueRecords[i];
                worksheet.Cell(i + 2, 1).Value = record.Username;
                worksheet.Cell(i + 2, 2).Value = record.BookTitle;
                worksheet.Cell(i + 2, 3).Value = record.BorrowDate.ToString("yyyy-MM-dd");
                worksheet.Cell(i + 2, 4).Value = record.DueDate.ToString("yyyy-MM-dd");
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);

            return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "OverdueRecords.xlsx");
        }
    }
}