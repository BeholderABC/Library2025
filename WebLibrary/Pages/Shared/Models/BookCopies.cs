namespace WebLibrary.Pages.Shared.Models
{
    public class BookCopy
    {
        // 对应 COPY.COPY_ID
        public int CopyId { get; set; }
        // 对应 COPY.BOOK_ID
        public int BookId { get; set; }
        // 对应 COPY.STATUS
        public string? Status { get; set; }
        // 对应 COPY.SHELF_LOCATION
        public string? ShelfLocation { get; set; }
        // 对应 COPY.CREATED_BY
        public string? CreatedBy { get; set; }
        // 对应 COPY.CREATED_AT
        public DateTime? CreatedAt { get; set; }
    }
}
