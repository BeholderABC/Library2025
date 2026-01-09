using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using WebLibrary.Pages.Shared.Utils;
using Microsoft.Extensions.Configuration;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WebLibrary.Pages.Admin
{
    [Authorize(Roles = "图书馆管理员")]
    public class UserManagementModel : PageModel
    {
        private readonly IConfiguration _config;
        public UserManagementModel(IConfiguration config) => _config = config;
        public List<UserInfo> Users { get; set; } = new List<UserInfo>();

        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 10;

        [BindProperty]public string NewPassword { get; set; }
        [BindProperty]
        public string Reason { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? UserTypeFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public class UserInfo
        {
            public int UserId { get; set; }
            public string UserName { get; set; } = "";
            public string? Email { get; set; } = "";
            public string UserType { get; set; } = "学生";
            public bool IsActive { get; set; } = true;
        }

        [BindProperty]
        public int UserId { get; set; }

        [BindProperty]
        public bool ForceChange { get; set; } = true;

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public void OnGet()
        {
            LoadUsers();
        }

        private void LoadUsers()
        {
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            // 构建基础查询
            string baseQuery = @"
                SELECT User_Id, User_Name, Email, User_Type, Status
                FROM Users 
                WHERE 1=1";

            if (!string.IsNullOrEmpty(SearchTerm))
                baseQuery += " AND (LOWER(User_Name) LIKE :search1 OR LOWER(Email) LIKE :search2)";

            if (!string.IsNullOrEmpty(UserTypeFilter))
                baseQuery += " AND User_Type = :userType";

            if (!string.IsNullOrEmpty(StatusFilter))
                baseQuery += " AND Status = :isActive";

            // 1) 先查询总记录数
            using var countCmd = new OracleCommand($"SELECT COUNT(*) FROM ({baseQuery})", conn);
            AddParameters(countCmd);
            int totalCount = Convert.ToInt32(countCmd.ExecuteScalar());

            // 2) 计算总页数并强制至少为 1
            TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
            // 3) Clamp 当前页码到 [1, TotalPages]
            CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);
            // 4) 计算分页偏移
            int offset = (CurrentPage - 1) * PageSize;

            // 分页查询
            string pagedQuery = $@"
                SELECT * 
                FROM (
                    SELECT a.*, ROWNUM rnum 
                    FROM ({baseQuery} ORDER BY user_id) a 
                    WHERE ROWNUM <= :endRow
                ) 
                WHERE rnum > :startRow";

            using var cmd = new OracleCommand(pagedQuery, conn);
            AddParameters(cmd);
            cmd.Parameters.Add("endRow", offset + PageSize);
            cmd.Parameters.Add("startRow", offset);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int userId = reader.GetInt32(0);
                if (userId == -1) continue;
                var UserName = reader.GetString(1);
                var Email = reader.IsDBNull(2) ? null : reader.GetString(2);
                var UserType = reader.GetString(3);
                var status = reader.IsDBNull(4) ? "" : reader.GetString(4);
                var IsActive = status == "Active";

                Users.Add(new UserInfo
                {
                    UserId = userId,
                    UserName = reader.GetString(1),
                    Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                    UserType = reader.GetString(3),
                    IsActive = reader.GetString(4) == "Active",
                });
            }
        }

        private void AddParameters(OracleCommand cmd)
        {
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                cmd.Parameters.Add("search1", $"%{SearchTerm.ToLower()}%");
                cmd.Parameters.Add("search2", $"%{SearchTerm.ToLower()}%");
            }
            if (!string.IsNullOrEmpty(UserTypeFilter))
                cmd.Parameters.Add("userType", UserTypeFilter);
            if (!string.IsNullOrEmpty(StatusFilter))
                cmd.Parameters.Add("isActive", StatusFilter == "active" ? "Active" : "Banned");
        }
        public IActionResult OnPostResetPassword()
        {
            // 获取当前管理员ID
            var adminId = User.FindFirst("UserId")?.Value;
            if (adminId == null)
            {
                TempData["ErrorMessage"] = "管理员身份验证失败";
                return RedirectToPage();
            }

            // 生成随机密码
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            using var transaction = conn.BeginTransaction();
            try
            {
                // 更新用户密码
                using var updateCmd = new OracleCommand(
                    @"UPDATE Users SET password = :pwd
                    WHERE user_id = :id", conn);
                updateCmd.Parameters.Add("pwd", PasswordHelper.HashPassword(NewPassword));
                updateCmd.Parameters.Add("id", UserId);
                updateCmd.ExecuteNonQuery();

                transaction.Commit();

                // 获取用户名用于显示
                using var nameCmd = new OracleCommand(
                    "SELECT user_name FROM Users WHERE user_id = :id", conn);
                nameCmd.Parameters.Add("id", UserId);
                string userName = nameCmd.ExecuteScalar()?.ToString() ?? "用户";

                TempData["SuccessMessage"] = $"用户 {userName} 的密码已重置！新密码为: <strong>{NewPassword}</strong>。请告知用户及时修改密码。";
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["ErrorMessage"] = $"密码重置失败: {ex.Message}";
            }

            return RedirectToPage();
        }
        /*
        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!@#$%^&*";
            var rng = new Random();
            return new string(Enumerable.Repeat(chars, 10)
                .Select(s => s[rng.Next(s.Length)]).ToArray());
        }
        */
        public IActionResult OnPostBanUser(int userId)
        {
            var adminId = User.FindFirst("UserId")?.Value;
            if (adminId == null)
            {
                TempData["ErrorMessage"] = "管理员身份验证失败";
                return RedirectToPage();
            }

            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            try
            {
                // 更新用户状态
                using var cmd = new OracleCommand(
                    "UPDATE Users SET Status = :status, ban_reason = :reason WHERE User_Id = :id", conn);
                cmd.Parameters.Add("status", "Banned");
                cmd.Parameters.Add("reason", Reason ?? (object)DBNull.Value);
                cmd.Parameters.Add("id", userId);
                cmd.ExecuteNonQuery();
                TempData["SuccessMessage"] = $"用户已成功禁用";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"状态更新失败: {ex.Message}";
            }
            return RedirectToPage();
        }

        public IActionResult OnPostActivateUser(int userId)
        {
            var adminId = User.FindFirst("UserId")?.Value;
            if (adminId == null)
            {
                TempData["ErrorMessage"] = "管理员身份验证失败";
                return RedirectToPage();
            }

            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            try
            {
                // 更新用户状态
                using var cmd = new OracleCommand(
                    "UPDATE Users SET Status = :status WHERE User_Id = :id", conn);
                cmd.Parameters.Add("status", "Active");
                cmd.Parameters.Add("id", userId);
                cmd.ExecuteNonQuery();
                TempData["SuccessMessage"] = $"用户已成功解禁";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"状态更新失败: {ex.Message}";
            }
            return RedirectToPage();
        }
    }
}