using System.Collections.Generic;
using System.Data;
using System.Linq;
using Scroll.Database;
using Scroll.Models;
using Scroll.Utilities;

namespace Scroll.Services
{
    using AuthorType = String;
    using BookType = Int64;
    using CategoryType = Int64;
    using CateNameType = String;
    using HotnessType = Single;
    using RatingType = Single;
    using TitleType = String;
    using UserType = Decimal;

    public interface ICurated
    {
        List<BookList> CuratedList(UserType userID);
    }

    public class Curated : ICurated
    {
        private readonly IScrollDB _repository;

        public Curated(IScrollDB repository)
        {
            _repository = repository;
        }

        public static DataTable RecommendBooksForUser(
            DataTable borrowTable,
            UserType userId)
        {
            // 1. 获取全站热门书籍（含热度值）
            DataTable hotBooksTable = Top.GetTopHotBooks(borrowTable);

            // 2. 获取所有用户的阅读偏好向量
            DataTable userVectorTable = Preference.GetDecayedUserCategoryVector(borrowTable);

            // 3. 查找当前用户向量
            DataRow userRow = userVectorTable.AsEnumerable()
                .FirstOrDefault(r => r.Field<UserType>("USER_ID") == userId);
            if (userRow == null)
                return new DataTable(); // 用户不存在或没借过书

            HotnessType[] userVector = (HotnessType[])userRow["CATEGORY_VECTOR"];

            // 4. 为每本书计算推荐值 = 用户兴趣值 * 热度
            var scoredBooks = hotBooksTable.AsEnumerable()
                .Select(row =>
                {
                    BookType bookId = row.Field<BookType>("BOOK_ID");
                    CategoryType categoryId = row.Field<CategoryType>("CATEGORY_ID");
                    CateNameType categoryName = row.Field<CateNameType>("CATEGORY_NAME");
                    HotnessType hotness = row.Field<HotnessType>("HOTNESS");
                    HotnessType userInterest = (categoryId < userVector.Length) ? userVector[categoryId] : 0;
                    HotnessType recommendScore = userInterest * hotness;
                    TitleType title = row.Field<TitleType>("TITLE");
                    TitleType author = row.Field<AuthorType>("AUTHOR");
                    Int64 borrowCount = row.Field<Int64>("BORROW_COUNT");
                    RatingType bookRating = row.Field<RatingType>("BOOK_RATING");
                    return new
                    {
                        BOOK_ID = bookId,
                        CATEGORY_ID = categoryId,
                        CATEGORY_NAME = categoryName,
                        TITLE = title,
                        AUTHOR = author,
                        BORROW_COUNT = borrowCount,
                        BOOK_RATING = bookRating,
                        RecommendScore = recommendScore,
                        //OriginalRow = row  // 保存原始行引用
                    };
                });

            // 5. 分离有推荐分数的书籍和无推荐分数的书籍
            var recommendedBooks = scoredBooks
                .Where(x => x.RecommendScore > 0)
                .OrderByDescending(x => x.RecommendScore);

            var nonRecommendedBooks = scoredBooks
                .Where(x => x.RecommendScore <= 0);

            // 6. 构造结果 DataTable
            DataTable result = new DataTable();
            result.Columns.Add("BOOK_ID", typeof(BookType));
            result.Columns.Add("CATEGORY_ID", typeof(CategoryType));
            result.Columns.Add("CATEGORY_NAME", typeof(CateNameType));
            result.Columns.Add("TITLE", typeof(TitleType));
            result.Columns.Add("AUTHOR", typeof(AuthorType));
            result.Columns.Add("BORROW_COUNT", typeof(Int64));
            result.Columns.Add("BOOK_RATING", typeof(RatingType));
            result.Columns.Add("HOTNESS", typeof(HotnessType));

            // 7. 先添加推荐书籍（按推荐分数降序）
            foreach (var item in recommendedBooks)
            {
                result.Rows.Add(item.BOOK_ID, item.CATEGORY_ID, item.CATEGORY_NAME, 
                               item.TITLE, item.AUTHOR, item.BORROW_COUNT, 
                               item.BOOK_RATING, item.RecommendScore);
            }

            // 8. 再添加剩余书籍（保持hotBooksTable中的原始顺序）
            foreach (var item in nonRecommendedBooks)
            {
                result.Rows.Add(item.BOOK_ID, item.CATEGORY_ID, item.CATEGORY_NAME, 
                               item.TITLE, item.AUTHOR, item.BORROW_COUNT, 
                               item.BOOK_RATING, item.RecommendScore);
            }

            return result;
        }

        public List<BookList> CuratedList(UserType userID)
        {
            var borrowTable = _repository.GetMergedBookRecords();
            var CuratedTable = RecommendBooksForUser(borrowTable, userID);
            var CuratedList = DataTableConverter.ConvertToBookList(CuratedTable);
            foreach (var book in CuratedList)
                book.CoverUrl = $"/api/bookcover/{book.BookID}";
            
            return CuratedList;
        }
    }
}