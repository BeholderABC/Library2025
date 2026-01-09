using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.Text.Unicode;
using System.Web;

namespace WebLibrary.Pages.Settings
{
    public class DataExtractModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public DataExtractModel(IConfiguration cfg) => _cfg = cfg;
        [BindProperty] public string ExtractType { get; set; } = "All";
        [BindProperty] public string ExtractFormat { get; set; } = "CSV";
        private bool IsAdmin => User.Identity?.IsAuthenticated == true && User.IsInRole("图书馆管理员");

        public IActionResult OnGet()
        {
            if (!IsAdmin)
            {
                return Redirect("/home");
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            if (!IsAdmin)
            {
                return Redirect("/home");
            }
            if (ExtractFormat == "CSV")
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                conn.Open();

                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream, encoding: System.Text.Encoding.UTF8);

                writer.WriteLine($"Extract Type,{ExtractType}");
                writer.WriteLine($"Extract Format,{ExtractFormat}");
                writer.WriteLine();

                if (ExtractType == "All" || ExtractType == "Books")
                {
                    writer.WriteLine("=== BOOK TABLE ===");
                    using (var cmd_book = new OracleCommand("SELECT * FROM ADMINISTRATOR.BOOK", conn))
                    using (var r_book = cmd_book.ExecuteReader())
                    {
                        var columnNames = new string[r_book.FieldCount];
                        for (int i = 0; i < r_book.FieldCount; i++)
                        {
                            columnNames[i] = r_book.GetName(i);
                        }
                        writer.WriteLine(string.Join(",", columnNames));

                        while (r_book.Read())
                        {
                            var values = new string[r_book.FieldCount];
                            for (int i = 0; i < r_book.FieldCount; i++)
                            {
                                var value = r_book[i]?.ToString() ?? "";
                                values[i] = value.Contains(",") ? $"\"{value}\"" : value;
                            }
                            writer.WriteLine(string.Join(",", values));
                        }
                    }
                }

                writer.WriteLine();

                if (ExtractType == "All" || ExtractType == "Users")
                {
                    writer.WriteLine("=== USERS TABLE ===");
                    using (var cmd_user = new OracleCommand("SELECT USER_ID,USER_NAME,USER_TYPE,EMAIL,STATUS,CREDIT_SCORE,IS_LIMITED FROM ADMINISTRATOR.USERS", conn))
                    using (var r_user = cmd_user.ExecuteReader())
                    {
                        var columnNames = new string[r_user.FieldCount];
                        for (int i = 0; i < r_user.FieldCount; i++)
                        {
                            columnNames[i] = r_user.GetName(i);
                        }
                        writer.WriteLine(string.Join(",", columnNames));

                        while (r_user.Read())
                        {
                            var values = new string[r_user.FieldCount];
                            for (int i = 0; i < r_user.FieldCount; i++)
                            {
                                var value = r_user[i]?.ToString() ?? "";
                                values[i] = value.Contains(",") ? $"\"{value}\"" : value;
                            }
                            writer.WriteLine(string.Join(",", values));
                        }
                    }
                }

                writer.Flush();

                var fileName = $"data_extract_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                return File(memoryStream.ToArray(), "text/csv", fileName);
            }
            else if (ExtractFormat == "JSON")
            {
                using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
                conn.Open();

                using var memoryStream = new MemoryStream();
                using var writer = new StreamWriter(memoryStream, encoding: System.Text.Encoding.UTF8);

                writer.WriteLine($"Extract Type,{ExtractType}");
                writer.WriteLine($"Extract Format,{ExtractFormat}");
                writer.WriteLine();

                writer.Flush();
                var jsonData = new List<Dictionary<string, object>>();
                if (ExtractType == "All" || ExtractType == "Books")
                {
                    using (var cmd_book = new OracleCommand("SELECT * FROM ADMINISTRATOR.BOOK", conn))
                    using (var r_book = cmd_book.ExecuteReader())
                    {
                        while (r_book.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < r_book.FieldCount; i++)
                            {
                                row[r_book.GetName(i)] = r_book[i] ?? DBNull.Value;
                            }
                            jsonData.Add(row);
                        }
                    }
                }
                
                if (ExtractType == "All" || ExtractType == "Users")
                {
                    using (var cmd_user = new OracleCommand("SELECT USER_ID,USER_NAME,USER_TYPE,EMAIL,STATUS,CREDIT_SCORE,IS_LIMITED FROM ADMINISTRATOR.USERS", conn))
                    using (var r_user = cmd_user.ExecuteReader())
                    {
                        while (r_user.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < r_user.FieldCount; i++)
                            {
                                row[r_user.GetName(i)] = r_user[i] ?? DBNull.Value;
                            }
                            jsonData.Add(row);
                        }
                    }
                }

                var json = System.Text.Json.JsonSerializer.Serialize(jsonData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(UnicodeRanges.All)
                });
                var fileName = $"data_extract_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
            }
            return Page();
        }
    }
}
