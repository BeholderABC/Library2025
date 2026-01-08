using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebLibrary.Pages.Settings
{
    public class SettingsModel : PageModel
    {
        private bool IsAdmin { get; set; }
        public void OnGet()
        {
            IsAdmin = User.IsInRole("图书馆管理员");
            if (!User.Identity.IsAuthenticated)
            {
                RedirectToPage("/Account/Login");
            }
            if (!IsAdmin)
            {
                RedirectToPage("/Index");
            }
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToPage("/Account/Login");
        }
    }
}
