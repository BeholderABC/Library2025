using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;
using Scroll.Database;

namespace WebLibrary.Pages.Lecture
{
    public class LectureManagementModel : PageModel
    {
        private readonly DatabaseContext _dbContext;
        private readonly IConfiguration _configuration;
        private readonly ILogger<LectureManagementModel> _logger;

        public LectureManagementModel(DatabaseContext dbContext, IConfiguration configuration, ILogger<LectureManagementModel> logger)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _logger = logger;
        }

        [BindProperty]
        public List<WebLibrary.Pages.Shared.Models.Lecture> Lectures { get; set; } = new List<WebLibrary.Pages.Shared.Models.Lecture>();

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadLecturesAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            try
            {
                _logger.LogInformation("开始删除活动，ID: {Id}", id);
                
                using var connection = _dbContext.GetConnection();
                
                // 检查表是否存在
                if (!await TableExistsAsync(connection, "LECTURE"))
                {
                    _logger.LogWarning("尝试删除活动时发现LECTURE表不存在");
                    TempData["ErrorMessage"] = "活动表不存在，请先添加一个活动";
                    return RedirectToPage();
                }

                // 先删除关联预约记录，避免外键/子记录阻止删除
                try
                {
                    if (await TableExistsAsync(connection, "RESERVATION_LEC"))
                    {
                        var deleteReservationCmd = connection.CreateCommand();
                        deleteReservationCmd.CommandText = "DELETE FROM RESERVATION_LEC WHERE LEC_ID = :id";
                        deleteReservationCmd.Parameters.Add(":id", OracleDbType.Int32).Value = id;
                        var reservationRows = await deleteReservationCmd.ExecuteNonQueryAsync();
                        _logger.LogInformation("已删除关联预约记录: {Count} 条，讲座ID: {Id}", reservationRows, id);
                    }
                    else
                    {
                        _logger.LogInformation("RESERVATION_LEC 表不存在，跳过清理预约记录");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "清理预约记录时出错，讲座ID: {Id}", id);
                }

                var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM LECTURE WHERE ID = :id";
                command.Parameters.Add(":id", OracleDbType.Int32).Value = id;

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    _logger.LogInformation("活动删除成功，ID: {Id}", id);
                    TempData["SuccessMessage"] = "活动删除成功！";
                }
                else
                {
                    _logger.LogWarning("未找到要删除的活动，ID: {Id}", id);
                    TempData["ErrorMessage"] = "未找到指定的活动";
                }
                
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除活动失败，ID: {Id}", id);
                TempData["ErrorMessage"] = $"删除失败：{ex.Message}";
                return RedirectToPage();
            }
        }

        private async Task LoadLecturesAsync()
        {
            try
            {
                using var connection = _dbContext.GetConnection();
                
                // 检查表是否存在
                if (!await TableExistsAsync(connection, "LECTURE"))
                {
                    _logger.LogInformation("LECTURE表不存在，显示提示信息");
                    TempData["InfoMessage"] = "活动表不存在，请先添加一个活动来创建表";
                    return;
                }

                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT ID, NAME, LECTURE_DATE, SPEAKER, SUMMARY, PICTURE, MAX_NUM, NOW_NUM 
                    FROM LECTURE 
                    ORDER BY LECTURE_DATE DESC";

                _logger.LogInformation("开始查询LECTURE表数据");
                using var reader = await command.ExecuteReaderAsync();
                int count = 0;
                while (await reader.ReadAsync())
                {
                    count++;
                    
                    var lecture = new WebLibrary.Pages.Shared.Models.Lecture
                    {
                        Id = Convert.ToInt32(reader["ID"]),
                        Name = reader["NAME"].ToString(),
                        LectureDate = Convert.ToDateTime(reader["LECTURE_DATE"]),
                        Speaker = reader["SPEAKER"].ToString(),
                        Summary = reader["SUMMARY"] == DBNull.Value ? null : reader["SUMMARY"].ToString(),
                        MaxNum = reader["MAX_NUM"] == DBNull.Value ? 100 : Convert.ToInt32(reader["MAX_NUM"]),
                        NowNum = reader["NOW_NUM"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NOW_NUM"])
                    };

                    // 处理图片数据
                    if (reader["PICTURE"] != DBNull.Value)
                    {
                        var pictureData = (byte[])reader["PICTURE"];
                        lecture.Picture = pictureData;
                    }

                    Lectures.Add(lecture);
                }
                
                _logger.LogInformation("成功加载 {Count} 条活动记录", count);
                
                // 只在没有活动数据时显示信息消息，避免重复的成功提示
                if (count == 0)
                {
                    TempData["InfoMessage"] = "暂无活动记录";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载活动数据失败");
                TempData["ErrorMessage"] = $"加载活动数据失败：{ex.Message}";
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
    }
} 