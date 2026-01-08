using System.Collections.Generic;
using System.Data;
using System.Linq;
using Scroll.Utilities;
using Scroll.Database;
using Scroll.Models;
using System.DirectoryServices.Protocols;


namespace Scroll.Services
{
    using AuthorType = String;
    using BookType = Int64;
    using CategoryType = Int64;
    using CateNameType = String;
    using HotnessType = Single;
    using RatingType = Single;
    using TitleType = String;

    public interface ITop
    {
        List<BookList> TopList();

        List<BookList> TopCList(CategoryType categoryID);
    }

    public class Top : ITop
    {
        private readonly IScrollDB _repository;

        public Top(IScrollDB repository)
        {
            _repository = repository;
        }

        private static void FillNullRatingsWithZero(DataTable borrowTable)
        {
            foreach (DataRow row in borrowTable.Rows)
            {
                if (row.IsNull("BOOK_RATING"))
                {
                    row["BOOK_RATING"] = 0.0;
                }
            }
        }

        private static double GetBookHotness(int borrowCount, double avgRating)
        {
            return Math.Pow(1 + avgRating, 0.7) * Math.Log(1 + borrowCount);
        }

        public static DataTable GetTopHotBooks(DataTable borrowTable)
        {
            //FillNullRatingsWithZero(borrowTable);

            //var filteredRows = borrowTable.AsEnumerable()
            //    .Where(row => {
            //        DateTime borrowDate = row.Field<DateTime>("BORROW_DATE");
            //        return borrowDate >= startDate && borrowDate <= endDate;
            //    });

            //var grouped = filteredRows
            var grouped = borrowTable.AsEnumerable()
                .GroupBy(row => row.Field<BookType>("BOOK_ID"))
                .Select(g => new {
                    BOOK_ID = g.Key,
                    CATEGORY_ID = g.First().Field<CategoryType>("CATEGORY_ID"),
                    CATEGORY_NAME = g.First().Field<CateNameType>("CATEGORY_NAME"),
                    TITLE = g.First().Field<TitleType>("TITLE"),
                    AUTHOR = g.First().Field<AuthorType>("AUTHOR"),
                    BORROW_COUNT = g.Count(),
                    BOOK_RATING = g.Average(r => r.Field<RatingType>("BOOK_RATING")),
                    HOTNESS = GetBookHotness(g.Count(), g.Average(r => r.Field<RatingType>("BOOK_RATING")))
                })
                .OrderByDescending(x => x.HOTNESS);

            // 构造返回的 DataTable
            DataTable result = new DataTable();
            result.Columns.Add("BOOK_ID", typeof(BookType));
            result.Columns.Add("CATEGORY_ID", typeof(CategoryType));
            result.Columns.Add("CATEGORY_NAME", typeof(CateNameType));
            result.Columns.Add("TITLE", typeof(TitleType));
            result.Columns.Add("AUTHOR", typeof(AuthorType));
            result.Columns.Add("BORROW_COUNT", typeof(Int64));
            result.Columns.Add("BOOK_RATING", typeof(RatingType));
            result.Columns.Add("HOTNESS", typeof(HotnessType));

            foreach (var item in grouped)
            {
                result.Rows.Add(
                    item.BOOK_ID, 
                    item.CATEGORY_ID,
                    item.CATEGORY_NAME,
                    item.TITLE,
                    item.AUTHOR,
                    item.BORROW_COUNT, 
                    item.BOOK_RATING, 
                    item.HOTNESS
                );
            }

            return result;
        }

        public static DataTable GetTopKHotBooksByCategory(DataTable hotBooksTable, CategoryType categoryID, int k)
        {
            var grouped = hotBooksTable.AsEnumerable()
                .Where(row => row.Field<CategoryType>("CATEGORY_ID") == categoryID)
                .GroupBy(row => row.Field<CategoryType>("CATEGORY_ID"))
                .SelectMany(g => g
                    .OrderByDescending(r => r.Field<HotnessType>("HOTNESS"))
                    .Take(k)
                );

            // 构造返回的 DataTable
            DataTable result = hotBooksTable.Clone(); // 拷贝表结构

            foreach (var row in grouped)
            {
                result.ImportRow(row);
            }

            return result;
        }

        public List<BookList> TopList()
        {
            var borrowTable = _repository.GetMergedBookRecords();
            var topTable = GetTopHotBooks(borrowTable);
            //DataTablePrinter.PrintDataTable(topTable);
            var topList = DataTableConverter.ConvertToBookList(topTable);
            foreach (var book in topList)
                book.CoverUrl = $"/api/bookcover/{book.BookID}";
            return topList;
        }

        public List<BookList> TopCList(CategoryType categoryID)
        {
            var borrowTable = _repository.GetMergedBookRecords();
            var hotBooksTable = GetTopHotBooks(borrowTable);
            var topCTable = GetTopKHotBooksByCategory(hotBooksTable, categoryID, 100);
            var topCList = DataTableConverter.ConvertToBookList(topCTable);
            foreach (var book in topCList)
                book.CoverUrl = $"/api/bookcover/{book.BookID}";
            return topCList;
        }
    }
}
