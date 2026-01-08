using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using WebLibrary.Pages.Shared.Models;

namespace WebLibrary.Pages.Admin
{
    public class BookCopiesModel : PageModel
    {
        private readonly IConfiguration _cfg;
        public BookCopiesModel(IConfiguration cfg) => _cfg = cfg;

        [BindProperty(SupportsGet = true)]
        public int BookId { get; set; }

        [BindProperty]
        public int NewCopyCount { get; set; } = 1;

        public Book Book { get; set; } = new();
        public List<BookCopy> BookCopies { get; set; } = new();

        // 状态常量
        private const string STATUS_AVAILABLE = "AVAILABLE";
        private const string STATUS_BORROWED = "BORROWED";
        private const string STATUS_REPAIRING = "REPAIRING";
        private const string STATUS_DISCARDED = "DISCARDED";

        public void OnGet()
        {
            LoadBook();
            LoadBookCopies();
        }

        /* ============== 单副本操作 ============== */

        // 借出
        public IActionResult OnPostBorrow(int copyId)
        {
            UpdateStatus(copyId, STATUS_BORROWED);
            return RedirectToPage("/Admin/BookCopies", new { bookId = BookId });
        }

        // 软删除（注销）副本
        public IActionResult OnPostDelete(int copyId)
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            /* 1. 检查是否已借出 */
            int loanedCount;
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText = "SELECT COUNT(*) FROM ADMINISTRATOR.COPY WHERE COPY_ID = :copyId AND STATUS = :borrowed";
                cmd.Parameters.Add("copyId", copyId);
                cmd.Parameters.Add("borrowed", STATUS_BORROWED);
                loanedCount = Convert.ToInt32(cmd.ExecuteScalar());
            }

            if (loanedCount > 0)
            {
                ReloadWithError("该副本当前处于借出状态，无法删除。", "delete");
                return Page();                 // 留在当前页
            }

            /* 2. 软删除：改为注销 */
            using (var cmd = conn.CreateCommand())
            {
                cmd.BindByName = true;
                cmd.CommandText = "UPDATE ADMINISTRATOR.COPY SET STATUS = :status WHERE COPY_ID = :copyId";
                cmd.Parameters.Add("status", STATUS_DISCARDED);
                cmd.Parameters.Add("copyId", copyId);
                cmd.ExecuteNonQuery();
            }

            SyncTotalAndAvailableCopies(conn);
            return RedirectToPage("/Admin/BookCopies", new { bookId = BookId });
        }

        public IActionResult OnPostSetRepairing(int copyId)
        {
            UpdateStatus(copyId, STATUS_REPAIRING);
            return RedirectToPage(new { bookId = BookId });
        }

        public IActionResult OnPostSetAvailable(int copyId)
        {
            UpdateStatus(copyId, STATUS_AVAILABLE);
            return RedirectToPage(new { bookId = BookId });
        }

        /* ============== 批量新增副本 ============== */
        public IActionResult OnPostAddMultipleCopies()
        {
            if (NewCopyCount <= 0 || NewCopyCount > 100)
            {
                ModelState.AddModelError("NewCopyCount", "请输入 1-100 之间的数字");
                LoadBook();
                LoadBookCopies();
                return Page();
            }

            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = @"
INSERT ALL
";
            for (int i = 0; i < NewCopyCount; i++)
                cmd.CommandText += " INTO ADMINISTRATOR.COPY (COPY_ID, BOOK_ID, STATUS, SHELF_LOCATION, CREATED_BY, CREATED_AT) VALUES (COPY_SEQ.NEXTVAL, :bookId, :status, :location, :createdBy, SYSDATE)";

            cmd.CommandText += " SELECT 1 FROM DUAL";
            cmd.Parameters.Add("bookId", BookId);
            cmd.Parameters.Add("status", STATUS_AVAILABLE);
            cmd.Parameters.Add("location", "默认位置");
            cmd.Parameters.Add("createdBy", User.Identity?.Name ?? "SYSTEM");
            cmd.ExecuteNonQuery();

            SyncTotalAndAvailableCopies(conn);
            return RedirectToPage("/Admin/BookCopies", new { bookId = BookId });
        }

        /* ============== 私有辅助 ============== */

        private void UpdateStatus(int copyId, string status)
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = "UPDATE ADMINISTRATOR.COPY SET STATUS = :status WHERE COPY_ID = :copyId";
            cmd.Parameters.Add("status", status);
            cmd.Parameters.Add("copyId", copyId);
            cmd.ExecuteNonQuery();

            SyncAvailableCopies(conn);
        }

        private void SyncAvailableCopies(OracleConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = @"
UPDATE ADMINISTRATOR.BOOK
   SET AVAILABLE_COPIES = (
       SELECT COUNT(*) FROM ADMINISTRATOR.COPY
        WHERE BOOK_ID = :bookId
          AND STATUS = :available)
 WHERE BOOK_ID = :bookId";
            cmd.Parameters.Add("bookId", BookId);
            cmd.Parameters.Add("available", STATUS_AVAILABLE);
            cmd.ExecuteNonQuery();
        }

        private void SyncTotalAndAvailableCopies(OracleConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = @"
UPDATE ADMINISTRATOR.BOOK
   SET TOTAL_COPIES = (
       SELECT COUNT(*) FROM ADMINISTRATOR.COPY
        WHERE BOOK_ID = :bookId
          AND STATUS <> :discarded),
       AVAILABLE_COPIES = (
       SELECT COUNT(*) FROM ADMINISTRATOR.COPY
        WHERE BOOK_ID = :bookId
          AND STATUS = :available)
 WHERE BOOK_ID = :bookId";
            cmd.Parameters.Add("bookId", BookId);
            cmd.Parameters.Add("discarded", STATUS_DISCARDED);
            cmd.Parameters.Add("available", STATUS_AVAILABLE);
            cmd.ExecuteNonQuery();
        }

        private void LoadBook()
        {
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = @"SELECT b.BOOK_ID, b.TITLE, b.AUTHOR, b.ISBN, b.TOTAL_COPIES,
                                       b.AVAILABLE_COPIES, c.CATEGORY_NAME
                                  FROM ADMINISTRATOR.BOOK b
                             LEFT JOIN ADMINISTRATOR.CATEGORY c
                                    ON b.CATEGORY_ID = c.CATEGORY_ID
                                 WHERE b.BOOK_ID = :bookId";
            cmd.Parameters.Add("bookId", BookId);
            using var rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                Book.BookId = rdr.GetInt32(0);
                Book.Title = rdr.GetString(1);
                Book.Author = rdr.GetString(2);
                Book.ISBN = rdr.GetString(3);
                Book.TotalCopies = rdr.GetInt32(4);
                Book.AvailableCopies = rdr.GetInt32(5);
                Book.CategoryName = rdr.IsDBNull(6) ? "—" : rdr.GetString(6);
            }
        }

        private void LoadBookCopies()
        {
            BookCopies.Clear();
            using var conn = new OracleConnection(_cfg.GetConnectionString("OracleDb"));
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.BindByName = true;
            cmd.CommandText = @"
SELECT COPY_ID, BOOK_ID, STATUS, SHELF_LOCATION, CREATED_BY, CREATED_AT
  FROM ADMINISTRATOR.COPY
 WHERE BOOK_ID = :bookId
   AND STATUS <> :discarded
 ORDER BY COPY_ID";
            cmd.Parameters.Add("bookId", BookId);
            cmd.Parameters.Add("discarded", STATUS_DISCARDED);

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                BookCopies.Add(new BookCopy
                {
                    CopyId = Convert.ToInt32(rdr["COPY_ID"]),
                    BookId = Convert.ToInt32(rdr["BOOK_ID"]),
                    Status = rdr.GetString("STATUS"),
                    ShelfLocation = rdr.IsDBNull("SHELF_LOCATION") ? null : rdr.GetString("SHELF_LOCATION"),
                    CreatedBy = rdr.IsDBNull("CREATED_BY") ? null : rdr.GetString("CREATED_BY"),
                    CreatedAt = rdr.IsDBNull("CREATED_AT") ? (DateTime?)null : rdr.GetDateTime("CREATED_AT")
                });
            }
        }

        public string? ErrorMessage { get; set; }
        public string? ErrorModal { get; set; }

        private void ReloadWithError(string msg, string? modal)
        {
            ErrorMessage = msg;
            ErrorModal = modal;   // "delete" 或 null
            LoadBook();
            LoadBookCopies();
        }
    }
}