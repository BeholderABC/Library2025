using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using WebLibrary.Pages.Shared.Models;
using Scroll.Database;

namespace WebLibrary.Pages.Lecture
{
    public class EditLectureModel : PageModel
    {
        private readonly DatabaseContext _dbContext;
        private readonly IConfiguration _configuration;

        public EditLectureModel(DatabaseContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        [BindProperty]
        public WebLibrary.Pages.Shared.Models.Lecture? Lecture { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            try
            {
                using var connection = _dbContext.GetConnection();
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT ID, NAME, LECTURE_DATE, SPEAKER, SUMMARY, PICTURE, MAX_NUM, NOW_NUM 
                    FROM LECTURE 
                    WHERE ID = :id";

                command.Parameters.Add(":id", OracleDbType.Int32).Value = id;

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    Lecture = new WebLibrary.Pages.Shared.Models.Lecture
                    {
                        Id = Convert.ToInt32(reader["ID"]),
                        Name = reader["NAME"].ToString(),
                        LectureDate = Convert.ToDateTime(reader["LECTURE_DATE"]),
                        Speaker = reader["SPEAKER"].ToString(),
                        Summary = reader["SUMMARY"] == DBNull.Value ? null : reader["SUMMARY"].ToString(),
                        MaxNum = reader["MAX_NUM"] == DBNull.Value ? 100 : Convert.ToInt32(reader["MAX_NUM"]),
                        NowNum = reader["NOW_NUM"] == DBNull.Value ? 0 : Convert.ToInt32(reader["NOW_NUM"])
                    };
                    
                    // 添加调试信息
                    System.Diagnostics.Debug.WriteLine($"加载活动信息 - ID: {Lecture.Id}, 名称: {Lecture.Name}, 日期: {Lecture.LectureDate}");

                    // 处理图片数据
                    if (reader["PICTURE"] != DBNull.Value)
                    {
                        var pictureData = (byte[])reader["PICTURE"];
                        Lecture.Picture = pictureData;
                    }
                }
                else
                {
                    Lecture = null;
                }

                return Page();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"加载活动信息失败：{ex.Message}";
                return RedirectToPage("/Lecture/LectureManagement");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid || Lecture == null)
            {
                return Page();
            }

            try
            {
                // 添加调试信息
                System.Diagnostics.Debug.WriteLine($"开始更新活动，ID: {Lecture.Id}");
                System.Diagnostics.Debug.WriteLine($"活动名称: {Lecture.Name}");
                System.Diagnostics.Debug.WriteLine($"主讲人: {Lecture.Speaker}");
                System.Diagnostics.Debug.WriteLine($"活动日期: {Lecture.LectureDate}");
                
                using var connection = _dbContext.GetConnection();
                var command = connection.CreateCommand();
                
                // 处理图片文件
                byte[]? pictureData = null;
                bool hasNewPicture = false;
                if (Request.Form.Files.Count > 0)
                {
                    var file = Request.Form.Files[0];
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream);
                        pictureData = memoryStream.ToArray();
                        hasNewPicture = true;
                    }
                }

                // 更新活动信息
                if (hasNewPicture)
                {
                    command.CommandText = @"
                        UPDATE LECTURE 
                        SET NAME = :name, LECTURE_DATE = :lectureDate, SPEAKER = :speaker, 
                            SUMMARY = :summary, PICTURE = :picture, MAX_NUM = :maxNum, NOW_NUM = :nowNum 
                        WHERE ID = :id";
                    
                    // 按照SQL中参数出现的顺序添加参数
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
                    
                    command.Parameters.Add(":picture", OracleDbType.Blob).Value = pictureData;
                    command.Parameters.Add(":maxNum", OracleDbType.Int32).Value = Lecture.MaxNum;
                    command.Parameters.Add(":nowNum", OracleDbType.Int32).Value = Lecture.NowNum;
                    command.Parameters.Add(":id", OracleDbType.Int32).Value = Lecture.Id;
                }
                else
                {
                    command.CommandText = @"
                        UPDATE LECTURE 
                        SET NAME = :name, LECTURE_DATE = :lectureDate, SPEAKER = :speaker, 
                            SUMMARY = :summary, MAX_NUM = :maxNum, NOW_NUM = :nowNum 
                        WHERE ID = :id";
                    
                    // 按照SQL中参数出现的顺序添加参数
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
                    
                    command.Parameters.Add(":maxNum", OracleDbType.Int32).Value = Lecture.MaxNum;
                    command.Parameters.Add(":nowNum", OracleDbType.Int32).Value = Lecture.NowNum;
                    command.Parameters.Add(":id", OracleDbType.Int32).Value = Lecture.Id;
                }

                var rowsAffected = await command.ExecuteNonQueryAsync();
                
                if (rowsAffected > 0)
                {
                    TempData["SuccessMessage"] = "活动更新成功！";
                }
                else
                {
                    TempData["ErrorMessage"] = "未找到指定的活动或更新失败";
                }
                
                return RedirectToPage("/Lecture/LectureManagement");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新活动异常: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                ModelState.AddModelError("", $"更新活动失败：{ex.Message}");
                return Page();
            }
        }
    }
} 