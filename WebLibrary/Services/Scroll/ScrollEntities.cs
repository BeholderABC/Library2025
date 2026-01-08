using System;

namespace Scroll.Models
{
    using AuthorType = String;
    using BookType = Int64;
    using CategoryType = Int64;
    using CateNameType = String;
    using HotnessType = Single;
    using RatingType = Single;
    using StatusType = String;
    using TitleType = String;
    using UrlType = String;
    using UserType = Decimal;

    public class BorrowList
    {
        // USER_ID, 
        // BORROW_DATE,
        // BOOK_ID,
        // TITLE,
        // CATEGORY_ID,
        // BOOK_RATING,
        // COVER
        public UserType UserID { get; set; }
        public DateTime BorrowDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime ReturnDate { get; set; }
        public StatusType? Status { get; set; }
        public BookType BookID { get; set; }
        public TitleType? Title { get; set; }
        public AuthorType? Author { get; set; }
        public CategoryType CategoryID { get; set; }
        public CateNameType? CategoryName { get; set; }
        public RatingType BookRating { get; set; }
        public UrlType? CoverUrl { get; set; }

        public TimeSpan BorrowTimeSpan => Status?.ToLower() switch
        {
            "reserved" => BorrowDate - DateTime.Now,
            "lending" => DueDate - DateTime.Now,
            "returned" => TimeSpan.Zero,
            "overdue" => DateTime.Now - DueDate,
        };
    }

    public class BookList
    {
        public BookType BookID { get; set; }
        public CategoryType CategoryID { get; set; }
        public CateNameType? CategoryName { get; set; }
        public TitleType? Title { get; set; }
        public AuthorType? Author { get; set; }
        public Int64 BorrowCount { get; set; }
        public RatingType Rating { get; set; }
        public HotnessType Score { get; set; }
        public UrlType? CoverUrl { get; set; }
    }

}