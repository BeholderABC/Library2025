using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using WebLibrary.Pages.Shared.Utils;  // NotificationService

namespace WebLibrary.Pages.Admin
{
    public class NotificationManagementModel : PageModel
    {
        public List<string> Roles { get; } = new() { "学生", "图书馆管理员", "其他教职工" };

        [BindProperty] public string TargetType { get; set; } = "All"; // All/Role/User
        [BindProperty] public string? Role { get; set; }               // 角色
        [BindProperty] public string? UserId { get; set; }             // 指定用户ID
        [BindProperty] public new string Content { get; set; } = "";       // 通知内容
        [BindProperty] public string ReceiverName { get; set; } = "";
           


        private readonly NotificationService _notificationService;
        private readonly IConfiguration _config;
        public NotificationManagementModel(IConfiguration config, NotificationService notificationService)
        {
            _notificationService = notificationService;
            _config = config;
        }

        public void OnGet()
        {
            // nothing
        }
        private (string?, string?) FindUserIdAndRoleByName(string name)
        {
            string? userId = null;
            string? role = null;
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT USER_ID, USER_TYPE FROM USERS WHERE USER_NAME = :name";
            cmd.Parameters.Add("name", name);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                userId = reader.GetString(0);
                role = reader.GetString(1);
            }
            Console.WriteLine($"UserId: {userId}, Role: {role}");
            return (userId, role);
        }

        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(Content))
            {
                TempData["Message"] = "请填写通知内容";
                return Page();
            }
            var senderId = User.FindFirst("UserId")?.Value;//获取当前管理员ID
            switch (TargetType)
            {
                case "All":
                    _notificationService.SendToAll(Content,senderId,"所有人");

                    TempData["Message"] = "已发送给所有用户。";

                    break;

                case "Role":
                    if (string.IsNullOrEmpty(Role))
                    {
                        TempData["Message"] = "请选择一个角色。";
                 }
                    else
                    {
                        _notificationService.SendToRole(Role, Content,senderId,Role);

                        TempData["Message"] = $"已发送给角色「{Role}」。";
                  }
                    break;

                //case "User":
                // Role 需要通过表单绑定，否则为 null，确保它有值ֵ
                /*
                if (!string.IsNullOrEmpty(Role) && !string.IsNullOrEmpty(UserId))
                {
                    _notificationService.SendToUser(roleCn: Role, userId: UserId, content: Content,senderId);
                }
                else
                {
                    TempData["Message"] = "请填写角色和用户ID。";
                }
                break;



                // 单发时通过姓名找 ID
                if (!string.IsNullOrWhiteSpace(UserId))
                {
                    var resolvedUserId = FindUserIdByName(ReceiverName, Role);
                    if (resolvedUserId == null)
                    {
                        TempData["ErrorMessage"] = "找不到该接收者，请确认姓名与角色正确。";
                        return Page();
                    }

                    _notificationService.SendToUser(
                        roleCn: Role,
                        userId: resolvedUserId,
                        content: Content,
                        senderId: senderId!,
                        receiverName: ReceiverName
                    );
                    Console.WriteLine($"User ID: {resolvedUserId}");
                }*/
                case "User":
                    
                    if (string.IsNullOrWhiteSpace(ReceiverName))
                    {

                        TempData["ErrorMessage"] = "请填写接收人姓名。";

                        return Page();
                    }
                    //var resolvedUserId, resolvedRole = FindUserIdByName(ReceiverName, Role!);
                    var resolvedUserId = FindUserIdAndRoleByName(ReceiverName).Item1;
                    var resolvedRole = FindUserIdAndRoleByName(ReceiverName).Item2;
                    if (resolvedUserId == null)
                    {
                        TempData["ErrorMessage"] = "找不到该接收者，请确认姓名与角色正确。";
                      return Page();
                    }
                    _notificationService.SendToUser(
                        roleCn: resolvedRole,
                        userId: resolvedUserId,
                        content: Content,
                        senderId: senderId!,
                        receiverName: ReceiverName
                    );

                    TempData["Message"] = $"已发送给用户「{ReceiverName}」。";
                  break;


                    

                default:

                    TempData["Message"] = "未知的接收类型。";
                    break;
            }

            return RedirectToPage();
        }

       
    }
}
