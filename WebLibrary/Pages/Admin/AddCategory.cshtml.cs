using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;

namespace WebLibrary.Pages.Admin
{
    public class AddCategoryModel : PageModel
    {
        private readonly IConfiguration _cfg;

        public AddCategoryModel(IConfiguration cfg) => _cfg = cfg;

        [BindProperty, Required, StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
                return Page();

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            // 检查是否已存在
            using var chk = conn.CreateCommand();
            chk.CommandText = "SELECT 1 FROM CATEGORY WHERE CATEGORY_NAME = :name";
            chk.Parameters.Add("name", CategoryName);
            if (chk.ExecuteScalar() != null)
            {
                ModelState.AddModelError("CategoryName", "该分类名称已存在");
                return Page();
            }

            // 插入新分类
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO CATEGORY (CATEGORY_ID, CATEGORY_NAME) VALUES (CATEGORY_SEQ.NEXTVAL, :name)";
            cmd.Parameters.Add("name", CategoryName);
            cmd.ExecuteNonQuery();

            return RedirectToPage("/Admin/BookManagement");
        }

    }
}

