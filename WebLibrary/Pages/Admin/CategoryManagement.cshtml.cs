using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace WebLibrary.Pages.Admin
{
    public class CategoriesModel : PageModel
    {
        private readonly IConfiguration _cfg;

        public CategoriesModel(IConfiguration cfg) => _cfg = cfg;

        public List<Category> Categories { get; set; } = new();

        public void OnGet()
        {
            LoadCategories();
        }

        public IActionResult OnPostCreate(string categoryName)
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();

            // 获取下一个 ID
            cmd.CommandText = "SELECT CATEGORY_SEQ.NEXTVAL FROM DUAL";
            var nextId = Convert.ToInt32(cmd.ExecuteScalar());

            // 插入新分类
            cmd.CommandText = "INSERT INTO CATEGORY (CATEGORY_ID, CATEGORY_NAME) VALUES (:id, :name)";
            cmd.Parameters.Add("id", nextId);
            cmd.Parameters.Add("name", categoryName);
            cmd.ExecuteNonQuery();

            return RedirectToPage();
        }

        public IActionResult OnPostEdit(int id, string categoryName)
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE CATEGORY SET CATEGORY_NAME = :name WHERE CATEGORY_ID = :id";
            cmd.Parameters.Add("name", categoryName);
            cmd.Parameters.Add("id", id);
            cmd.ExecuteNonQuery();
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int id)
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            /* 1. 检查是否被 BOOK 引用 */
            int refCount;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM BOOK WHERE CATEGORY_ID = :id";
                cmd.Parameters.Add("id", id);
                refCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (refCount > 0)
            {
                ReloadWithError($"无法删除：该分类下仍有 {refCount} 本图书，请先调整或删除相关图书。", "delete");
                return Page();          // 留在当前页
            }

            /* 2. 未被引用，真正删除 */
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM CATEGORY WHERE CATEGORY_ID = :id";
                cmd.Parameters.Add("id", id);
                cmd.ExecuteNonQuery();
            }

            return RedirectToPage();
        }

        private void LoadCategories()
        {
            Categories.Clear();
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT CATEGORY_ID, CATEGORY_NAME FROM CATEGORY ORDER BY CATEGORY_NAME";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                Categories.Add(new Category
                {
                    CategoryId = rdr.GetInt32(0),
                    CategoryName = rdr.GetString(1)
                });
            }
        }

        private void ReloadWithError(string msg, string? modal)
        {
            ErrorMessage = msg;
            ErrorModal = modal;          // 传 null 或 "delete" 均可
            LoadCategories();
        }

        public string? ErrorMessage { get; set; }
        public string? ErrorModal { get; set; }
    }

}

public class Category
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

