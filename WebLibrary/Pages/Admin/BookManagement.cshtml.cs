using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using WebLibrary.Pages.Shared.Models;

namespace WebLibrary.Pages.Admin
{
    public class BookManagementModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public BookManagementModel(IConfiguration cfg) => _cfg = cfg;

        public List<Book> Books { get; set; } = new();
        public List<SelectListItem> CategoryOptions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public NewBookInputModel NewBook { get; set; } = new();

        /* ============ 检索 & 分页参数 ============ */
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; private set; }
        private const int PageSize = 9;
        /* ========================================= */

        public string? ErrorMessage { get; set; }
        public string? ErrorModal { get; set; }

        public class NewBookInputModel
        {
            public int BookId { get; set; }

            [Required] public string Title { get; set; } = "";
            [Required] public string Author { get; set; } = "";
            [Required] public string ISBN { get; set; } = "";
            public string? Publisher { get; set; }
            [DataType(DataType.Date)] public DateTime? PublicationDate { get; set; }
            [Required] public int? CategoryId { get; set; }
            [Required, Range(0, int.MaxValue)] public int? TotalCopies { get; set; }
            public string? Description { get; set; }
        }

        public void OnGet()
        {
            LoadCategories();
            LoadBooks();
        }

        /* ---------------- 新增 ---------------- */
        public IActionResult OnPostCreate()
        {
            ModelState.Remove("SearchTerm");
            ModelState.Remove("StatusFilter");
            ModelState.Remove("CategoryFilter");

            if (!ModelState.IsValid)
            {
                var errors = string.Join("; ", ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => $"{x.Key}: {string.Join(",", x.Value.Errors.Select(e => e.ErrorMessage))}"));

                ReloadWithError($"验证失败: {errors}", "create");
                return Page();
            }

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            // ISBN 唯一性检查
            using (var chk = conn.CreateCommand())
            {
                chk.BindByName = true;
                chk.CommandText = "SELECT 1 FROM ADMINISTRATOR.BOOK WHERE ISBN = :isbn";
                chk.Parameters.Add("isbn", NewBook.ISBN);
                if (chk.ExecuteScalar() != null)
                {
                    ReloadWithError("ISBN 已存在，无法重复添加。", "create");
                    return Page();
                }
            }

            /* 1. 先插入 BOOK（占位 0，下面立即更新） */
            const string sql = @"
INSERT INTO ADMINISTRATOR.BOOK
 (BOOK_ID, TITLE, AUTHOR, ISBN, PUBLISHER,
  PUBLICATION_DATE, CATEGORY_ID, DESCRIPTION,
  TOTAL_COPIES, AVAILABLE_COPIES)
VALUES
 (BOOK_SEQ.NEXTVAL,
  :title, :author, :isbn, :publisher,
  :pubDate, :catId, :descr,
  0, 0)
RETURNING BOOK_ID INTO :bookId";
            int bookId;
            using (var cmd = new OracleCommand(sql, conn) { BindByName = true })
            {
                cmd.Parameters.Add("title", NewBook.Title);
                cmd.Parameters.Add("author", NewBook.Author);
                cmd.Parameters.Add("isbn", NewBook.ISBN);
                cmd.Parameters.Add("publisher", (object?)NewBook.Publisher ?? DBNull.Value);
                cmd.Parameters.Add("pubDate", NewBook.PublicationDate ?? (object)DBNull.Value);
                cmd.Parameters.Add("catId", NewBook.CategoryId!.Value);
                cmd.Parameters.Add("descr", (object?)NewBook.Description ?? DBNull.Value);
                var bookIdParam = cmd.Parameters.Add("bookId", OracleDbType.Decimal, 0, ParameterDirection.Output);
                cmd.ExecuteNonQuery();
                bookId = (int)(OracleDecimal)bookIdParam.Value;
            }

            /* 2. 批量插入副本 */
            for (int i = 0; i < NewBook.TotalCopies!.Value; i++)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
INSERT INTO ADMINISTRATOR.COPY
(COPY_ID, BOOK_ID, STATUS, SHELF_LOCATION, CREATED_BY, CREATED_AT)
VALUES (BOOK_COPY_SEQ.NEXTVAL, :bookId, 'AVAILABLE', :loc, :creator, SYSDATE)";
                cmd.Parameters.Add("bookId", bookId);
                cmd.Parameters.Add("loc", "默认位置");
                cmd.Parameters.Add("creator", GetCurrentUserId());
                cmd.ExecuteNonQuery();
            }

            /* 3. 实时回填 BOOK 两列 */
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE ADMINISTRATOR.BOOK
   SET TOTAL_COPIES     = (SELECT COUNT(*) FROM ADMINISTRATOR.COPY WHERE BOOK_ID = :id),
       AVAILABLE_COPIES = (SELECT COUNT(*) FROM ADMINISTRATOR.COPY WHERE BOOK_ID = :id AND STATUS = 'AVAILABLE')
 WHERE BOOK_ID = :id";
                cmd.Parameters.Add("id", bookId);
                cmd.ExecuteNonQuery();
            }

            return RedirectToPage();
        }

        /* ---------------- 编辑 ---------------- */
        public IActionResult OnPostEdit()
        {
            /* -------------- 1. 原有验证 -------------- */
            ModelState.Remove("SearchTerm");
            ModelState.Remove("StatusFilter");
            ModelState.Remove("CategoryFilter");

            if (!ModelState.IsValid || NewBook.BookId <= 0)
            {
                var errors = string.Join("; ",
                    ModelState.Values.SelectMany(v => v.Errors)
                                   .Select(e => e.ErrorMessage));
                ReloadWithError($"验证失败: {errors}", "edit");
                return Page();
            }

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            /* -------------- 2. 取原始 TOTAL_COPIES -------------- */
            int originalTotal;
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText = "SELECT TOTAL_COPIES FROM ADMINISTRATOR.BOOK WHERE BOOK_ID = :id";
                cmd.Parameters.Add("id", NewBook.BookId);
                var r = cmd.ExecuteScalar();
                if (r == null || r == DBNull.Value)
                {
                    ReloadWithError("未找到要编辑的图书。", "edit");
                    return Page();
                }
                originalTotal = Convert.ToInt32(r);
            }

            /* -------------- 3. ISBN 重复校验 -------------- */
            using (var dup = conn.CreateCommand())
            {
                dup.BindByName = true;
                dup.CommandText = "SELECT 1 FROM ADMINISTRATOR.BOOK WHERE ISBN = :isbn AND BOOK_ID <> :id";
                dup.Parameters.Add("isbn", NewBook.ISBN);
                dup.Parameters.Add("id", NewBook.BookId);
                if (dup.ExecuteScalar() != null)
                {
                    ReloadWithError("ISBN 已存在，无法重复使用。", "edit");
                    return Page();
                }
            }

            int change = NewBook.TotalCopies!.Value - originalTotal;

            /* -------------- 4. 减少副本 -------------- */
            if (change < 0)
            {
                int toRemove = -change;
                using var cmd = conn.CreateCommand();
                cmd.BindByName = true;
                cmd.CommandText = @"
DELETE FROM ADMINISTRATOR.COPY
WHERE COPY_ID IN (
    SELECT COPY_ID FROM (
        SELECT COPY_ID
          FROM ADMINISTRATOR.COPY
         WHERE BOOK_ID = :bookId
           AND STATUS = 'AVAILABLE'
           AND COPY_ID NOT IN (SELECT COPY_ID FROM ADMINISTRATOR.BORROWRECORD)
         ORDER BY COPY_ID
    )
    WHERE ROWNUM <= :cnt)";
                cmd.Parameters.Add("bookId", NewBook.BookId);
                cmd.Parameters.Add("cnt", toRemove);
                int deleted = cmd.ExecuteNonQuery();
                if (deleted < toRemove)
                {
                    ReloadWithError("无法减少副本：部分副本已被借出或不存在足够可删除副本。", "edit");
                    return Page();
                }
            }

            /* -------------- 5. 增加副本（含序列安全校验）-------------- */
            if (change > 0)
            {
                /* 5.1 拿到当前最大 COPY_ID */
                int maxId;
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT NVL(MAX(COPY_ID),0) FROM ADMINISTRATOR.COPY";
                    maxId = Convert.ToInt32(cmd.ExecuteScalar());
                }

                /* 5.2 把序列调到 maxId+1*/
                int nextSeq = maxId + 1;
                using (var cmd = new OracleCommand("ALTER SEQUENCE BOOK_COPY_SEQ RESTART START WITH " + nextSeq, conn))
                {
                    cmd.ExecuteNonQuery();
                }

                /* 5.3 循环插入新副本 */
                for (int i = 0; i < change; i++)
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
INSERT INTO ADMINISTRATOR.COPY
(COPY_ID, BOOK_ID, STATUS, SHELF_LOCATION, CREATED_BY, CREATED_AT)
VALUES
(BOOK_COPY_SEQ.NEXTVAL, :bookId, 'AVAILABLE', :loc, :creater, SYSDATE)";
                    cmd.Parameters.Add("bookId", NewBook.BookId);
                    cmd.Parameters.Add("loc", "默认位置");
                    cmd.Parameters.Add("creater", GetCurrentUserId());
                    cmd.ExecuteNonQuery();
                }
            }

            /* -------------- 6. 更新图书主表 -------------- */
            const string upd = @"
UPDATE ADMINISTRATOR.BOOK
   SET TITLE = :title,
       AUTHOR = :author,
       ISBN = :isbn,
       PUBLISHER = :publisher,
       PUBLICATION_DATE = :pubDate,
       CATEGORY_ID = :catId,
       DESCRIPTION = :descr,
       TOTAL_COPIES = :total,
       AVAILABLE_COPIES = (
           SELECT COUNT(*) FROM ADMINISTRATOR.COPY
            WHERE BOOK_ID = :id AND STATUS = 'AVAILABLE')
 WHERE BOOK_ID = :id";
            using (var cmd = new OracleCommand(upd, conn) { BindByName = true })
            {
                cmd.Parameters.Add("title", NewBook.Title);
                cmd.Parameters.Add("author", NewBook.Author);
                cmd.Parameters.Add("isbn", NewBook.ISBN);
                cmd.Parameters.Add("publisher",
                    string.IsNullOrEmpty(NewBook.Publisher) ? DBNull.Value : NewBook.Publisher);
                cmd.Parameters.Add("pubDate",
                    NewBook.PublicationDate.HasValue ? (object)NewBook.PublicationDate.Value : DBNull.Value);
                cmd.Parameters.Add("catId", NewBook.CategoryId!.Value);
                cmd.Parameters.Add("descr",
                    string.IsNullOrEmpty(NewBook.Description) ? DBNull.Value : NewBook.Description);
                cmd.Parameters.Add("total", NewBook.TotalCopies!.Value);
                cmd.Parameters.Add("id", NewBook.BookId);
                cmd.ExecuteNonQuery();
            }

            return RedirectToPage(new { SearchTerm, CategoryFilter, StatusFilter, CurrentPage });
        }

        /* ---------------- 删除 ---------------- */
        public IActionResult OnPostDelete(int id)
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            /* 1. 统计当前借出副本数 */
            int loanedCount;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM ADMINISTRATOR.COPY WHERE BOOK_ID = :bookId AND STATUS = 'BORROWED'";
                cmd.Parameters.Add("bookId", id);
                loanedCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (loanedCount > 0)
            {
                ReloadWithError($"无法删除：该图书仍有 {loanedCount} 个副本处于借出状态，请先处理归还。", null);
                return Page();
            }
            
            /* 2. 删除从未被借出的副本 */
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
DELETE FROM ADMINISTRATOR.COPY
WHERE BOOK_ID = :bookId
  AND COPY_ID NOT IN (SELECT COPY_ID FROM ADMINISTRATOR.BORROWRECORD)";
                cmd.Parameters.Add("bookId", id);
                int deleted = cmd.ExecuteNonQuery();

                if (deleted == 0)
                {
                    ReloadWithError("该图书所有副本都已有借阅记录，无法删除。", null);
                    return Page();
                }
            }

            /* 3. 再删图书本身 */
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM ADMINISTRATOR.BOOK WHERE BOOK_ID = :id";
                cmd.Parameters.Add("id", id);
                cmd.ExecuteNonQuery();
            }

            return RedirectToPage();
        }
        
        /* ---------------- 新增分类 ---------------- */
        public IActionResult OnPostCreateCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                ReloadWithError("分类名称不能为空", null);
                return Page();
            }

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = @"
INSERT INTO ADMINISTRATOR.CATEGORY (CATEGORY_ID, CATEGORY_NAME)
SELECT CATEGORY_SEQ.NEXTVAL, :name
  FROM dual
 WHERE NOT EXISTS (
       SELECT 1 FROM ADMINISTRATOR.CATEGORY WHERE CATEGORY_NAME = :name
)";
            cmd.Parameters.Add("name", categoryName);
            var rows = cmd.ExecuteNonQuery();

            if (rows == 0)
            {
                ReloadWithError("该分类已存在", null);
                return Page();
            }

            return RedirectToPage();
        }

        /* ======================================================
         * 私有方法：分页 + 筛选
         * ====================================================== */
        private void LoadBooks()
        {
            Books.Clear();

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            /* ---- 先算总数 ---- */
            string countSql = @"
SELECT COUNT(*)
  FROM ADMINISTRATOR.BOOK b
  LEFT JOIN ADMINISTRATOR.CATEGORY c ON b.CATEGORY_ID = c.CATEGORY_ID
 WHERE 1 = 1";

            var parameters = new List<OracleParameter>();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                countSql += " AND (LOWER(b.TITLE) LIKE LOWER(:search) OR LOWER(b.AUTHOR) LIKE LOWER(:search) OR LOWER(b.ISBN) LIKE LOWER(:search))";
                parameters.Add(new OracleParameter("search", $"%{SearchTerm.Trim()}%"));
            }

            if (!string.IsNullOrWhiteSpace(CategoryFilter) && int.TryParse(CategoryFilter, out int catId))
            {
                countSql += " AND b.CATEGORY_ID = :catId";
                parameters.Add(new OracleParameter("catId", catId));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                countSql += StatusFilter == "available"
                    ? " AND b.AVAILABLE_COPIES > 0"
                    : " AND b.AVAILABLE_COPIES = 0";
            }

            int totalRecords;
            using (var cmd = new OracleCommand(countSql, conn))
            {
                cmd.BindByName = true;
                foreach (var param in parameters)
                {
                    cmd.Parameters.Add(param);
                }
                totalRecords = Convert.ToInt32(cmd.ExecuteScalar());
            }

            TotalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)PageSize));
            CurrentPage = Math.Clamp(CurrentPage, 1, TotalPages);

            /* ---- 取分页数据 ---- */
            string sql = @"
SELECT b.BOOK_ID, b.TITLE, b.AUTHOR, b.ISBN, b.PUBLISHER,
       b.PUBLICATION_DATE, b.CATEGORY_ID, b.DESCRIPTION,
       b.TOTAL_COPIES, b.AVAILABLE_COPIES, c.CATEGORY_NAME
  FROM ADMINISTRATOR.BOOK b
  LEFT JOIN ADMINISTRATOR.CATEGORY c
    ON b.CATEGORY_ID = c.CATEGORY_ID
 WHERE 1 = 1";

            // 重置参数列表
            parameters = new List<OracleParameter>();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                sql += " AND (LOWER(b.TITLE) LIKE LOWER(:search) OR LOWER(b.AUTHOR) LIKE LOWER(:search) OR LOWER(b.ISBN) LIKE LOWER(:search))";
                parameters.Add(new OracleParameter("search", $"%{SearchTerm.Trim()}%"));
            }

            if (!string.IsNullOrWhiteSpace(CategoryFilter) && int.TryParse(CategoryFilter, out catId))
            {
                sql += " AND b.CATEGORY_ID = :catId";
                parameters.Add(new OracleParameter("catId", catId));
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                sql += StatusFilter == "available"
                    ? " AND b.AVAILABLE_COPIES > 0"
                    : " AND b.AVAILABLE_COPIES = 0";
            }

            sql += " ORDER BY b.TITLE";
            sql += " OFFSET :skip ROWS FETCH NEXT :take ROWS ONLY";

            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.BindByName = true;

                // 添加筛选参数
                foreach (var param in parameters)
                {
                    cmd.Parameters.Add(param);
                }

                // 添加分页参数
                cmd.Parameters.Add("skip", (CurrentPage - 1) * PageSize);
                cmd.Parameters.Add("take", PageSize);

                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    Books.Add(new Book
                    {
                        BookId = rdr.GetInt32(0),
                        Title = rdr.GetString(1),
                        Author = rdr.GetString(2),
                        ISBN = rdr.GetString(3),
                        Publisher = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                        PublicationDate = rdr.IsDBNull(5) ? null : rdr.GetDateTime(5),
                        CategoryId = rdr.IsDBNull(6) ? 0 : rdr.GetInt32(6),
                        Description = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                        TotalCopies = rdr.GetInt32(8),
                        AvailableCopies = rdr.GetInt32(9),
                        CategoryName = rdr.IsDBNull(10) ? "—" : rdr.GetString(10)
                    });
                }
            }
        }

        private void LoadCategories()
        {
            CategoryOptions.Clear();
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();
            const string sql = "SELECT CATEGORY_ID, CATEGORY_NAME FROM ADMINISTRATOR.CATEGORY ORDER BY CATEGORY_NAME";
            using var cmd = new OracleCommand(sql, conn);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                CategoryOptions.Add(new SelectListItem
                {
                    Value = rdr.GetInt32(0).ToString(),
                    Text = rdr.GetString(1)
                });
            }
        }

        private void ReloadWithError(string msg, string? modal)
        {
            ErrorMessage = msg;
            ErrorModal = modal;
            LoadCategories();
            LoadBooks();
        }

        private int GetCurrentUserId()
        {
            // 尝试从 Claims 中读取 UserId
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;

            // 如果未登录或解析失败，返回系统默认账号 ID（如 1）
            return 1;
        }
    }
}