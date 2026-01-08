using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;
using Scroll.Database;
using System.Security.Claims;

namespace WebLibrary.Pages.Lecture
{
    public class LectureActivitiesModel : PageModel
    {
        private readonly DatabaseContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LectureActivitiesModel> _logger;

        public LectureActivitiesModel(DatabaseContext dbContext, IConfiguration configuration, ILogger<LectureActivitiesModel> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        public List<LectureInfo> FutureLectures { get; set; } = new List<LectureInfo>();
        public List<ReservationInfo> MyReservations { get; set; } = new List<ReservationInfo>();
        public bool IsAuthenticated { get; set; }
        public int CurrentUserId { get; set; }

        public class LectureInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Speaker { get; set; } = string.Empty;
            public DateTime LectureDate { get; set; }
            public string? Summary { get; set; }
            public byte[]? Picture { get; set; }
            public int MaxNum { get; set; }
            public int NowNum { get; set; }
            public bool CanReserve { get; set; }
            public bool IsReserved { get; set; }
        }

        public class ReservationInfo
        {
            public int Id { get; set; }
            public int LecId { get; set; }
            public int UserId { get; set; }
            public string LectureName { get; set; } = string.Empty;
            public string Speaker { get; set; } = string.Empty;
            public DateTime LectureDate { get; set; }
            public DateTime ReservationDate { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // 检查用户是否已登录
                IsAuthenticated = User.Identity?.IsAuthenticated == true;
                
                if (IsAuthenticated)
                {
                    var userIdClaim = User.FindFirst("UserId");
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        CurrentUserId = userId;
                    }
                    else
                    {
                        _logger.LogWarning("无法获取用户ID，声明值: {ClaimValue}", userIdClaim?.Value);
                    }
                }

                // 加载讲座数据
                await LoadFutureLecturesAsync();
                
                if (IsAuthenticated)
                {
                    await LoadMyReservationsAsync();
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载讲座活动页面时发生错误: {Message}", ex.Message);
                
                // 设置默认值，确保页面能正常显示
                FutureLectures = new List<LectureInfo>();
                MyReservations = new List<ReservationInfo>();
                
                TempData["ErrorMessage"] = $"加载页面时发生错误：{ex.Message}";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostReserveAsync(int lectureId)
        {
            // 添加详细的认证状态日志
            _logger.LogInformation("预约请求 - 讲座ID: {LectureId}", lectureId);
            _logger.LogInformation("用户认证状态: {IsAuthenticated}", User.Identity?.IsAuthenticated);
            _logger.LogInformation("用户名称: {UserName}", User.Identity?.Name);
            
            // 检查所有可用的用户声明
            foreach (var claim in User.Claims)
            {
                _logger.LogInformation("用户声明: {Type} = {Value}", claim.Type, claim.Value);
            }

            // 直接检查用户认证状态
            if (!User.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("用户未认证，跳转登录页面");
                return RedirectToPage("/Account/Login");
            }

            // 获取当前用户ID
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                _logger.LogWarning("未找到UserId声明");
                // 尝试其他可能的用户ID声明
                userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub") ?? User.FindFirst("id");
            }
            
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int currentUserId))
            {
                _logger.LogError("无法获取用户ID，声明值: {ClaimValue}", userIdClaim?.Value);
                TempData["ErrorMessage"] = "无法获取用户信息，请重新登录";
                return RedirectToPage("/Account/Login");
            }

            _logger.LogInformation("成功获取用户ID: {UserId}", currentUserId);

            OracleConnection? connection = null;
            try
            {
                connection = _dbContext.GetConnection();
                _logger.LogInformation("数据库连接成功");
                
                // 检查讲座是否存在且可预约
                var lecture = await GetLectureByIdAsync(connection, lectureId);
                if (lecture == null)
                {
                    _logger.LogWarning("讲座不存在，讲座ID: {LectureId}", lectureId);
                    TempData["ErrorMessage"] = "讲座不存在";
                    return RedirectToPage();
                }

                _logger.LogInformation("找到讲座: {Name}, 当前人数: {NowNum}, 最大人数: {MaxNum}", 
                    lecture.Name, lecture.NowNum, lecture.MaxNum);

                if (lecture.NowNum >= lecture.MaxNum)
                {
                    _logger.LogWarning("讲座已满员，讲座ID: {LectureId}", lectureId);
                    TempData["ErrorMessage"] = "讲座已满员，无法预约";
                    return RedirectToPage();
                }

                // 检查用户是否已经预约过
                var hasReserved = await HasUserReservedAsync(connection, lectureId, currentUserId);
                _logger.LogInformation("用户是否已预约: {HasReserved}", hasReserved);
                
                if (hasReserved)
                {
                    _logger.LogWarning("用户已预约过此讲座，用户ID: {UserId}, 讲座ID: {LectureId}", currentUserId, lectureId);
                    TempData["ErrorMessage"] = "您已经预约过此讲座";
                    return RedirectToPage();
                }

                // 创建预约记录
                _logger.LogInformation("开始创建预约记录，用户ID: {UserId}, 讲座ID: {LectureId}", currentUserId, lectureId);
                await CreateReservationAsync(connection, lectureId, currentUserId);
                _logger.LogInformation("预约记录创建成功");
                
                // 更新讲座当前人数
                _logger.LogInformation("更新讲座人数，讲座ID: {LectureId}, 当前人数: {NowNum}, 增加: 1", lectureId, lecture.NowNum);
                await UpdateLectureNowNumAsync(connection, lectureId, lecture.NowNum + 1);
                _logger.LogInformation("讲座人数更新成功");

                TempData["SuccessMessage"] = "预约成功！";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "预约讲座失败，讲座ID: {LectureId}, 用户ID: {UserId}, 错误详情: {Message}", 
                    lectureId, currentUserId, ex.Message);
                
                // 提供更详细的错误信息
                var errorMessage = "预约失败";
                if (ex.InnerException != null)
                {
                    errorMessage += $": {ex.InnerException.Message}";
                }
                else
                {
                    errorMessage += $": {ex.Message}";
                }
                
                TempData["ErrorMessage"] = errorMessage;
                return RedirectToPage();
            }
            finally
            {
                // 使用共享连接，不在此处关闭或释放
            }
        }

        public async Task<IActionResult> OnPostCancelReservationAsync(int reservationId)
        {
            // 添加详细的认证状态日志
            _logger.LogInformation("取消预约请求 - 预约ID: {ReservationId}", reservationId);
            _logger.LogInformation("用户认证状态: {IsAuthenticated}", User.Identity?.IsAuthenticated);
            _logger.LogInformation("用户名称: {UserName}", User.Identity?.Name);
            
            // 检查所有可用的用户声明
            foreach (var claim in User.Claims)
            {
                _logger.LogInformation("用户声明: {Type} = {Value}", claim.Type, claim.Value);
            }

            // 直接检查用户认证状态
            if (!User.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("用户未认证，跳转登录页面");
                return RedirectToPage("/Account/Login");
            }

            // 获取当前用户ID
            var userIdClaim = User.FindFirst("UserId");
            if (userIdClaim == null)
            {
                _logger.LogWarning("未找到UserId声明");
                // 尝试其他可能的用户ID声明
                userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier) ?? User.FindFirst("sub") ?? User.FindFirst("id");
            }
            
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int currentUserId))
            {
                _logger.LogError("无法获取用户ID，声明值: {ClaimValue}", userIdClaim?.Value);
                TempData["ErrorMessage"] = "无法获取用户信息，请重新登录";
                return RedirectToPage("/Account/Login");
            }

            _logger.LogInformation("成功获取用户ID: {UserId}", currentUserId);

            OracleConnection? connection = null;
            try
            {
                connection = _dbContext.GetConnection();
                
                // 获取预约信息
                var reservation = await GetReservationByIdAsync(connection, reservationId);
                if (reservation == null)
                {
                    TempData["ErrorMessage"] = "预约记录不存在";
                    return RedirectToPage();
                }

                // 检查是否是当前用户的预约
                if (reservation.UserId != currentUserId)
                {
                    TempData["ErrorMessage"] = "您只能取消自己的预约";
                    return RedirectToPage();
                }

                // 取消预约
                await CancelReservationAsync(connection, reservationId);
                
                // 更新讲座当前人数
                await UpdateLectureNowNumAsync(connection, reservation.LecId, -1);

                TempData["SuccessMessage"] = "预约已取消";
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取消预约失败，预约ID: {ReservationId}", reservationId);
                TempData["ErrorMessage"] = "取消预约失败，请重试";
                return RedirectToPage();
            }
            finally
            {
                // 使用共享连接，不在此处关闭或释放
            }
        }

        private async Task LoadFutureLecturesAsync()
        {
            OracleConnection? connection = null;
            try
            {
                connection = _dbContext.GetConnection();
                
                // 检查LECTURE表是否存在
                if (!await TableExistsAsync(connection, "LECTURE"))
                {
                    return;
                }

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT ID, NAME, LECTURE_DATE, SPEAKER, SUMMARY, PICTURE, MAX_NUM, NOW_NUM 
                    FROM LECTURE 
                    WHERE LECTURE_DATE > SYSDATE - INTERVAL '1' DAY
                    ORDER BY LECTURE_DATE ASC";

                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    try
                    {
                        var lecture = new LectureInfo
                        {
                            Id = Convert.ToInt32(reader["ID"]),
                            Name = reader["NAME"].ToString() ?? string.Empty,
                            Speaker = reader["SPEAKER"].ToString() ?? string.Empty,
                            LectureDate = Convert.ToDateTime(reader["LECTURE_DATE"]),
                            Summary = reader["SUMMARY"] as string,
                            MaxNum = reader["MAX_NUM"] == DBNull.Value ? 100 : Convert.ToInt32(reader["MAX_NUM"]),
                            NowNum = reader["NOW_NUM"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NOW_NUM"])
                        };

                        if (reader["PICTURE"] != DBNull.Value)
                        {
                            lecture.Picture = (byte[])reader["PICTURE"];
                        }

                        // 判断是否可以预约
                        lecture.CanReserve = lecture.NowNum < lecture.MaxNum;
                        
                        // 判断用户是否已预约（如果已登录）
                        if (IsAuthenticated)
                        {
                            lecture.IsReserved = await HasUserReservedAsync(connection, lecture.Id, CurrentUserId);
                        }

                        FutureLectures.Add(lecture);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "处理讲座记录时出错");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载讲座失败: {Message}", ex.Message);
                throw new InvalidOperationException($"加载讲座失败: {ex.Message}", ex);
            }
            finally
            {
                // 使用共享连接，不在此处关闭或释放
            }
        }

        private async Task LoadMyReservationsAsync()
        {
            OracleConnection? connection = null;
            try
            {
                connection = _dbContext.GetConnection();
                
                // 检查RESERVATION_LEC表是否存在
                if (!await TableExistsAsync(connection, "RESERVATION_LEC"))
                {
                    return;
                }

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT r.ID, r.LEC_ID, r.USER_ID, r.RESERVATION_DATE, r.STATUS,
                           l.NAME as LECTURE_NAME, l.SPEAKER, l.LECTURE_DATE
                    FROM RESERVATION_LEC r
                    JOIN LECTURE l ON r.LEC_ID = l.ID
                    WHERE r.USER_ID = :userId AND r.STATUS = 'waiting'
                    ORDER BY r.RESERVATION_DATE DESC";

                command.Parameters.Add(":userId", OracleDbType.Int32).Value = CurrentUserId;

                using var reader = await command.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    try
                    {
                        var reservation = new ReservationInfo
                        {
                            Id = Convert.ToInt32(reader["ID"]),
                            LecId = Convert.ToInt32(reader["LEC_ID"]),
                            UserId = Convert.ToInt32(reader["USER_ID"]),
                            ReservationDate = Convert.ToDateTime(reader["RESERVATION_DATE"]),
                            Status = reader["STATUS"].ToString() ?? string.Empty,
                            LectureName = reader["LECTURE_NAME"].ToString() ?? string.Empty,
                            Speaker = reader["SPEAKER"].ToString() ?? string.Empty,
                            LectureDate = Convert.ToDateTime(reader["LECTURE_DATE"])
                        };

                        MyReservations.Add(reservation);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "处理预约记录时出错");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载用户预约记录失败: {Message}", ex.Message);
                throw new InvalidOperationException($"加载用户预约记录失败: {ex.Message}", ex);
            }
            finally
            {
                // 使用共享连接，不在此处关闭或释放
            }
        }

        private async Task<LectureInfo?> GetLectureByIdAsync(OracleConnection connection, int lectureId)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, NAME, LECTURE_DATE, SPEAKER, SUMMARY, PICTURE, MAX_NUM, NOW_NUM 
                FROM LECTURE 
                WHERE ID = :id";

            command.Parameters.Add(":id", OracleDbType.Int32).Value = lectureId;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LectureInfo
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    Name = reader["NAME"].ToString() ?? string.Empty,
                    Speaker = reader["SPEAKER"].ToString() ?? string.Empty,
                    LectureDate = Convert.ToDateTime(reader["LECTURE_DATE"]),
                    Summary = reader["SUMMARY"] as string,
                    MaxNum = reader["MAX_NUM"] == DBNull.Value ? 100 : Convert.ToInt32(reader["MAX_NUM"]),
                    NowNum = reader["NOW_NUM"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NOW_NUM"])
                };
            }
            return null;
        }

        private async Task<bool> HasUserReservedAsync(OracleConnection connection, int lectureId, int userId)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) FROM RESERVATION_LEC 
                WHERE LEC_ID = :lectureId AND USER_ID = :userId AND STATUS = 'waiting'";

            command.Parameters.Add(":lectureId", OracleDbType.Int32).Value = lectureId;
            command.Parameters.Add(":userId", OracleDbType.Int32).Value = userId;

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        private async Task CreateReservationAsync(OracleConnection connection, int lectureId, int userId)
        {
            try
            {
                // 检查RESERVATION_LEC表是否存在，如果不存在则创建
                if (!await TableExistsAsync(connection, "RESERVATION_LEC"))
                {
                    _logger.LogInformation("RESERVATION_LEC表不存在，开始创建");
                    await CreateReservationTableAsync(connection);
                    _logger.LogInformation("RESERVATION_LEC表创建成功");
                }

                // 检查序列是否存在
                if (!await SequenceExistsAsync(connection, "RESERVATION_LEC_SEQ"))
                {
                    _logger.LogInformation("RESERVATION_LEC_SEQ序列不存在，开始创建");
                    await CreateReservationSequenceAsync(connection);
                    _logger.LogInformation("RESERVATION_LEC_SEQ序列创建成功");
                }

                var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO RESERVATION_LEC (ID, LEC_ID, USER_ID, RESERVATION_DATE, STATUS) 
                    VALUES (RESERVATION_LEC_SEQ.NEXTVAL, :lecId, :userId, SYSDATE, 'waiting')";

                command.Parameters.Add(":lecId", OracleDbType.Int32).Value = lectureId;
                command.Parameters.Add(":userId", OracleDbType.Int32).Value = userId;

                _logger.LogInformation("执行预约插入SQL: {Sql}", command.CommandText);
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("预约记录插入成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建预约记录失败，讲座ID: {LectureId}, 用户ID: {UserId}", lectureId, userId);
                throw new InvalidOperationException($"创建预约记录失败: {ex.Message}", ex);
            }
        }

        private async Task<ReservationInfo?> GetReservationByIdAsync(OracleConnection connection, int reservationId)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ID, LEC_ID, USER_ID, RESERVATION_DATE, STATUS 
                FROM RESERVATION_LEC 
                WHERE ID = :id";

            command.Parameters.Add(":id", OracleDbType.Int32).Value = reservationId;

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ReservationInfo
                {
                    Id = Convert.ToInt32(reader["ID"]),
                    LecId = Convert.ToInt32(reader["LEC_ID"]),
                    UserId = Convert.ToInt32(reader["USER_ID"]),
                    ReservationDate = Convert.ToDateTime(reader["RESERVATION_DATE"]),
                    Status = reader["STATUS"].ToString() ?? string.Empty
                };
            }
            return null;
        }

        private async Task CancelReservationAsync(OracleConnection connection, int reservationId)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE RESERVATION_LEC 
                SET STATUS = 'cancelled' 
                WHERE ID = :id";

            command.Parameters.Add(":id", OracleDbType.Int32).Value = reservationId;

            await command.ExecuteNonQueryAsync();
        }

        private async Task UpdateLectureNowNumAsync(OracleConnection connection, int lectureId, int change)
        {
            try
            {
                var command = connection.CreateCommand();
                if (change > 0)
                {
                    command.CommandText = @"
                        UPDATE LECTURE 
                        SET NOW_NUM = NOW_NUM + :change 
                        WHERE ID = :id";
                }
                else
                {
                    command.CommandText = @"
                        UPDATE LECTURE 
                        SET NOW_NUM = GREATEST(0, NOW_NUM + :change) 
                        WHERE ID = :id";
                }

                command.Parameters.Add(":change", OracleDbType.Int32).Value = change;
                command.Parameters.Add(":id", OracleDbType.Int32).Value = lectureId;

                _logger.LogInformation("执行讲座人数更新SQL: {Sql}, 讲座ID: {LectureId}, 变化: {Change}", 
                    command.CommandText, lectureId, change);
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("讲座人数更新成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新讲座人数失败，讲座ID: {LectureId}, 变化: {Change}", lectureId, change);
                throw new InvalidOperationException($"更新讲座人数失败: {ex.Message}", ex);
            }
        }

        private async Task CreateReservationTableAsync(OracleConnection connection)
        {
            try
            {
                _logger.LogInformation("开始创建RESERVATION_LEC表");
                
                // 创建表
                var tableCommand = connection.CreateCommand();
                tableCommand.CommandText = @"
                    CREATE TABLE RESERVATION_LEC (
                        ID NUMBER PRIMARY KEY,
                        LEC_ID NUMBER NOT NULL,
                        USER_ID NUMBER NOT NULL,
                        RESERVATION_DATE DATE NOT NULL,
                        STATUS VARCHAR2(50)
                    )";
                
                _logger.LogInformation("创建表: {Sql}", tableCommand.CommandText);
                await tableCommand.ExecuteNonQueryAsync();
                _logger.LogInformation("表创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建RESERVATION_LEC表失败");
                throw new InvalidOperationException($"创建RESERVATION_LEC表失败: {ex.Message}", ex);
            }
        }

        private async Task CreateReservationSequenceAsync(OracleConnection connection)
        {
            try
            {
                _logger.LogInformation("开始创建RESERVATION_LEC_SEQ序列");
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE SEQUENCE RESERVATION_LEC_SEQ
                    START WITH 1
                    INCREMENT BY 1
                    NOCACHE
                    NOCYCLE";
                _logger.LogInformation("创建序列: {Sql}", command.CommandText);
                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("序列创建成功");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建RESERVATION_LEC_SEQ序列失败");
                throw new InvalidOperationException($"创建RESERVATION_LEC_SEQ序列失败: {ex.Message}", ex);
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
                _logger.LogWarning(ex, "检查表 {TableName} 是否存在时出错: {Message}", tableName, ex.Message);
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
                _logger.LogWarning(ex, "检查序列 {SequenceName} 是否存在时出错: {Message}", sequenceName, ex.Message);
                return false;
            }
        }
    }
} 