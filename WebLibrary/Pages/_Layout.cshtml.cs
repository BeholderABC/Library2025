using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using System.Security.Claims;
using WebLibrary.Pages.Shared.Utils;

namespace WebLibrary.Pages
{
    public class _LayoutModel : PageModel
    {
        private readonly NotificationStatusService _notificationStatusService;

        public _LayoutModel(NotificationStatusService notificationStatusService)
        {
            _notificationStatusService = notificationStatusService;
        }

        public string? UserName { get; set; }
        public bool IsAdmin { get; set; }

        public int UnreadCount { get; set; } = 0; // 新增属性

        public void OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                // 使用FindFirstValue简化代码，避免空引用错误
                UserName = User.FindFirstValue(ClaimTypes.Name);
                IsAdmin = User.FindFirstValue(ClaimTypes.Role) == "图书馆管理员";
                var userId = User.FindFirstValue("UserId");      // 你之前存的用户ID字段

                var role = User.FindFirstValue(ClaimTypes.Role); // 中文角色，如“学生”

                /*
                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(role))
                {
                    UnreadCount = _notificationStatusService.GetUnreadCount(userId, role);
                }

                */
                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(role))
                {
                    var count = _notificationStatusService.GetUnreadCount(userId, role);
                    UnreadCount = count;
                    ViewData["UnreadCount"] = count;
                }
                else
                {
                    UnreadCount = 0;
                    ViewData["UnreadCount"] = 0;
                }
                
            }
        }
    }
}