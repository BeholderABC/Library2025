using System.Collections.Generic;
using Scroll.Models;
using System.Data;

namespace Scroll.Utilities
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

    public static class DataTableConverter
    {
        public static List<BorrowList> ConvertToBorrowList(DataTable dataTable)
        {
            List<BorrowList> borrowRecords = new List<BorrowList>();

            foreach (DataRow row in dataTable.Rows)
            {
                BorrowList record = new BorrowList
                {
                    UserID = row["USER_ID"] is DBNull ? default(UserType) : (UserType)row["USER_ID"],
                    BorrowDate = row["BORROW_DATE"] is DBNull ? DateTime.MinValue : (DateTime)row["BORROW_DATE"],
                    DueDate = row["DUE_DATE"] is DBNull ? DateTime.MinValue : (DateTime)row["DUE_DATE"],
                    ReturnDate = row["RETURN_DATE"] is DBNull ? DateTime.MinValue : (DateTime)row["RETURN_DATE"],
                    Status = row["STATUS"] is DBNull ? default(String) : (StatusType)row["STATUS"],
                    BookID = row["BOOK_ID"] is DBNull ? default(BookType) : (BookType)row["BOOK_ID"],
                    Title = row["TITLE"] is DBNull ? default(TitleType) : (TitleType)row["TITLE"],
                    Author = row["AUTHOR"] is DBNull ? default(AuthorType) : (AuthorType)row["AUTHOR"],
                    CategoryID = row["CATEGORY_ID"] is DBNull ? default(CategoryType) : (CategoryType)row["CATEGORY_ID"],
                    CategoryName = row["CATEGORY_NAME"] is DBNull ? default(CateNameType) : (CateNameType)row["CATEGORY_NAME"],
                    BookRating = row["BOOK_RATING"] is DBNull ? default(RatingType) : (RatingType)row["BOOK_RATING"]
                };

                borrowRecords.Add(record);
            }

            return borrowRecords;
        }

        public static List<BookList> ConvertToBookList(DataTable dataTable)
        {
            List<BookList> bookLists = new List<BookList>();

            foreach (DataRow row in dataTable.Rows)
            {
                BookList book = new BookList
                {
                    BookID = row["BOOK_ID"] is DBNull ? default(BookType) : (BookType)row["BOOK_ID"],
                    CategoryID = row["CATEGORY_ID"] is DBNull ? default(CategoryType) : (CategoryType)row["CATEGORY_ID"],
                    CategoryName = row["CATEGORY_NAME"] is DBNull ? default(CateNameType) : (CateNameType)row["CATEGORY_NAME"],
                    Title = row["TITLE"] is DBNull ? default(TitleType) : (TitleType)row["TITLE"],
                    Author = row["AUTHOR"] is DBNull ? default(AuthorType) : (AuthorType)row["AUTHOR"],
                    BorrowCount = row["BORROW_COUNT"] is DBNull ? 0 : Convert.ToInt64(row["BORROW_COUNT"]),
                    Rating = row["BOOK_RATING"] is DBNull ? default(RatingType) : (RatingType)row["BOOK_RATING"],
                    Score = row["HOTNESS"] is DBNull ? default(HotnessType) : (HotnessType)row["HOTNESS"]
                };

                bookLists.Add(book);
            }

            return bookLists;
        }
    }
}