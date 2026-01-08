using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;
using Scroll.Database;
using System.Diagnostics;

namespace WebLibrary.Pages.Lecture
{
    public class AddLectureModel : PageModel
    {
        private readonly DatabaseContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AddLectureModel> _logger;

        public AddLectureModel(DatabaseContext dbContext, IConfiguration configuration, ILogger<AddLectureModel> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public WebLibrary.Pages.Shared.Models.Lecture Lecture { get; set; } = new WebLibrary.Pages.Shared.Models.Lecture();

        public IActionResult OnGet()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("模型验证失败: {Errors}", string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return Page();
            }

            try
            {
                _logger.LogInformation("开始添加活动: {Name}, {Speaker}, {Date}", Lecture.Name, Lecture.Speaker, Lecture.LectureDate);

                using var connection = _dbContext.GetConnection();
                _logger.LogInformation("数据库连接成功");

                // 测试数据库连接
                await TestDatabaseConnectionAsync(connection);
                
                // 检查LECTURE表是否存在
                if (!await TableExistsAsync(connection, "LECTURE"))
                {
                    _logger.LogInformation("LECTURE表不存在，开始创建表");
                    // 如果表不存在，创建表
                    await CreateLectureTableAsync(connection);
                    _logger.LogInformation("LECTURE表创建成功");
                }
                else
                {
                    _logger.LogInformation("LECTURE表已存在");
                }

                // 检查序列是否存在
                if (!await SequenceExistsAsync(connection, "LECTURE_SEQ"))
                {
                    _logger.LogInformation("LECTURE_SEQ序列不存在，开始创建序列");
                    // 如果序列不存在，创建序列
                    await CreateLectureSequenceAsync(connection);
                    _logger.LogInformation("LECTURE_SEQ序列创建成功");
                }
                else
                {
                    _logger.LogInformation("LECTURE_SEQ序列已存在");
                }

                var command = connection.CreateCommand();
                
                // 处理图片文件
                byte[]? pictureData = null;
                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[0];
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);
                        pictureData = memoryStream.ToArray();
                        _logger.LogInformation("图片文件处理成功，大小: {Size} bytes", pictureData.Length);
                    }
                }

                // 插入新活动
                command.CommandText = @"
                    INSERT INTO LECTURE (ID, NAME, LECTURE_DATE, SPEAKER, SUMMARY, PICTURE, MAX_NUM, NOW_NUM) 
                    VALUES (LECTURE_SEQ.NEXTVAL, :name, :lectureDate, :speaker, :summary, :picture, :maxNum, :nowNum)";

                command.Parameters.Add(":name", OracleDbType.Varchar2).Value = Lecture.Name;
                command.Parameters.Add(":lectureDate", OracleDbType.Date).Value = Lecture.LectureDate;
                command.Parameters.Add(":speaker", OracleDbType.Varchar2).Value = Lecture.Speaker;
                
                if (!string.IsNullOrEmpty(Lecture.Summary))
                {
                    command.Parameters.Add(":summary", OracleDbType.Clob).Value = Lecture.Summary;
                }
                else
                {
                    command.Parameters.Add(":summary", OracleDbType.Clob).Value = DBNull.Value;
                }

                if (pictureData != null)
                {
                    command.Parameters.Add(":picture", OracleDbType.Blob).Value = pictureData;
                }
                else
                {
                    command.Parameters.Add(":picture", OracleDbType.Blob).Value = DBNull.Value;
                }

                command.Parameters.Add(":maxNum", OracleDbType.Int32).Value = Lecture.MaxNum;
                command.Parameters.Add(":nowNum", OracleDbType.Int32).Value = Lecture.NowNum;

                _logger.LogInformation("执行SQL插入语句");
                var rowsAffected = await command.ExecuteNonQueryAsync();
                _logger.LogInformation("SQL执行成功，影响行数: {RowsAffected}", rowsAffected);

                TempData["SuccessMessage"] = "活动添加成功！";
                return RedirectToPage("/Lecture/LectureManagement");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "添加活动失败: {Message}", ex.Message);
                ModelState.AddModelError("", $"添加活动失败：{ex.Message}");
                return Page();
            }
        }

        private async Task TestDatabaseConnectionAsync(OracleConnection connection)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM DUAL";
                var result = await command.ExecuteScalarAsync();
                _logger.LogInformation("数据库连接测试成功，结果: {Result}", result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库连接测试失败");
                throw new InvalidOperationException("数据库连接测试失败", ex);
            }
        }

        private async Task<bool> TableExistsAsync(OracleConnection connection, string tableName)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = :tableName";
                command.Parameters.Add(":tableName", OracleDbType.Varchar2).Value = tableName.ToUpper();
                
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检查表 {TableName} 是否存在时出错", tableName);
                return false;
            }
        }

        private async Task<bool> SequenceExistsAsync(OracleConnection connection, string sequenceName)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT COUNT(*) FROM USER_SEQUENCES WHERE SEQUENCE_NAME = :sequenceName";
                command.Parameters.Add(":sequenceName", OracleDbType.Varchar2).Value = sequenceName.ToUpper();
                
                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "检查序列 {SequenceName} 是否存在时出错", sequenceName);
                return false;
            }
        }

        private async Task CreateLectureTableAsync(OracleConnection connection)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE LECTURE (
                        ID NUMBER PRIMARY KEY,
                        NAME VARCHAR2(255) NOT NULL,
                        LECTURE_DATE DATE NOT NULL,
                        SPEAKER VARCHAR2(255) NOT NULL,
                        SUMMARY CLOB,
                        PICTURE BLOB,
                        MAX_NUM NUMBER DEFAULT 0,
                        NOW_NUM NUMBER DEFAULT 0
                    )";
                
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("LECTURE表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建LECTURE表失败");
                throw new InvalidOperationException("创建LECTURE表失败", ex);
            }
        }

        private async Task CreateLectureSequenceAsync(OracleConnection connection)
        {
            try
            {
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE SEQUENCE LECTURE_SEQ
                    START WITH 1
                    INCREMENT BY 1
                    NOCACHE
                    NOCYCLE";
                
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("LECTURE_SEQ序列创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建LECTURE_SEQ序列失败");
                throw new InvalidOperationException("创建LECTURE_SEQ序列失败", ex);
            }
        }
    }
} 