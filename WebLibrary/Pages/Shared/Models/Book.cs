namespace WebLibrary.Pages.Shared.Models
{
    public class Book
    {
        public int? BookId { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? ISBN { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; } // For displaying category name from join
        public string? Publisher { get; set; }
        public DateTime? PublicationDate { get; set; }
        public string? Description { get; set; }
        public decimal? BookRating { get; set; }
        public int? TotalCopies { get; set; }
        public int? AvailableCopies { get; set; }
        public DateTime? CreateTime { get; set; }
        public DateTime? UpdateTime { get; set; }

    }
}
