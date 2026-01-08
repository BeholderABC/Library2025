namespace WebLibrary.Pages.Shared.Models
{
    public class BorrowRecord
    {
        // 对应 BorrowRecord 表的 record_id（主键）
        public int RecordId { get; set; }

        // 对应 user_id（关联用户表）
        public int UserId { get; set; }

        

        // 对应 book_id（关联书籍表）
        public int BookId { get; set; }

        
        // 对应 copy_id（图书副本ID）
        public int CopyId { get; set; }

        // 对应 borrow_date（借出日期）
        public DateTime BorrowDate { get; set; }

        // 对应 due_date（应还日期）
        public DateTime DueDate { get; set; }

        // 对应 status（借阅状态：lending/returned/overdue/overdue_returned）
        public string Status { get; set; } = string.Empty;

        // 对应 renew_times（续借次数）
        public int RenewTimes { get; set; }
    }
}
