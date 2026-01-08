using System.Collections.Generic;
using System.Data;
using System.Linq;
using DocumentFormat.OpenXml.Spreadsheet;
using Scroll.Database;
using Scroll.Models;
using Scroll.Utilities;

namespace Scroll.Services
{
    using UserType = Decimal;
    using CategoryType = Int64;
    using HotnessType = Single;
    using CateNameType = String;

    public interface IPreference
    {
        List<BorrowList> History(UserType userID);

        Dictionary<CateNameType, Int64> Interest(UserType userID);
    }

    public class Preference : IPreference
    {
        private readonly IScrollDB _repository;

        public Preference(IScrollDB repository)
        {
            _repository = repository;
        }

        public static DataTable GetUserBorrowHistory(DataTable borrowTable, UserType userId)
        {
            var filtered = borrowTable.AsEnumerable()
                .Where(row => row.Field<UserType>("USER_ID") == userId);

            DataTable result = borrowTable.Clone(); // 复制结构

            foreach (var row in filtered)
            {
                result.ImportRow(row);
            }

            return result;
        }

        // 将专门用于 Rebuild 用户兴趣向量表
        public static DataTable GetUserCategoryVector(DataTable borrowTable)
        {

            // 获取所有不同的 CATEGORY_ID（整数）
            var categorySet = borrowTable.AsEnumerable()
                .Select(row => row.Field<Int64>("CATEGORY_ID"))
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            int maxCategoryId = (int)categorySet.Max();
            int vectorSize = maxCategoryId + 1;

            // 构建用户向量映射
            Dictionary<UserType, Int64[]> userVectors = new Dictionary<UserType, Int64[]>();

            foreach (DataRow row in borrowTable.Rows)
            {
                UserType userId = row.Field<UserType>("USER_ID");
                CategoryType categoryId = Convert.ToInt64(row.Field<CategoryType>("CATEGORY_ID")); // ensure index is int

                if (!userVectors.ContainsKey(userId))
                {
                    userVectors[userId] = new Int64[vectorSize];
                }

                userVectors[userId][categoryId]++;
            }

            // 构造结果 DataTable
            DataTable result = new DataTable();
            result.Columns.Add("USER_ID", typeof(UserType));
            result.Columns.Add("CATEGORY_VECTOR", typeof(Int64[]));

            foreach (var kvp in userVectors)
            {
                DataRow row = result.NewRow();
                row["USER_ID"] = kvp.Key;
                row["CATEGORY_VECTOR"] = kvp.Value;
                result.Rows.Add(row);
            }

            return result;
        }

        // 时间权重计算函数
        private static HotnessType CalculateTimeWeight(DateTime borrowDate, DateTime currentTime)
        {
            /*
             * decayRate = 0.01
             * 1个月前：权重保持约74%
             * 3个月前：权重约41%
             * 半年前：权重约18%
             * 1年前：权重约3%
             * 
             * decayRate = 0.004
             * 1个月前：权重保持约89%
             * 3个月前：权重约70%
             * 半年前：权重约48%
             * 1年前：权重约23%
             */
            double decayRate = 0.004; // 衰减率
            double daysDiff = (currentTime - borrowDate).TotalDays;
            return (HotnessType)Math.Exp(-decayRate * daysDiff);
        }

        public static DataTable GetDecayedUserCategoryVector(DataTable borrowTable)
        {
            // 获取所有不同的 CATEGORY_ID（整数）
            var categorySet = borrowTable.AsEnumerable()
                .Select(row => row.Field<Int64>("CATEGORY_ID"))
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            int maxCategoryId = (int)categorySet.Max();
            int vectorSize = maxCategoryId + 1;

            // 获取当前时间用于计算时间差
            DateTime currentTime = DateTime.Now;

            // 构建用户向量映射（使用float类型来支持小数权重）
            Dictionary<UserType, HotnessType[]> userVectors = new Dictionary<UserType, HotnessType[]>();

            foreach (DataRow row in borrowTable.Rows)
            {
                UserType userId = row.Field<UserType>("USER_ID");
                CategoryType categoryId = Convert.ToInt64(row.Field<CategoryType>("CATEGORY_ID"));
                DateTime borrowDate = row.Field<DateTime>("BORROW_DATE");
    
                if (!userVectors.ContainsKey(userId))
                {
                    userVectors[userId] = new HotnessType[vectorSize];
                }
    
                // 计算时间权重
                HotnessType weight = CalculateTimeWeight(borrowDate, currentTime);
    
                // 累加权重到对应的类别位置
                userVectors[userId][categoryId] += weight;
            }

            // 构造结果 DataTable
            DataTable result = new DataTable();
            result.Columns.Add("USER_ID", typeof(UserType));
            result.Columns.Add("CATEGORY_VECTOR", typeof(HotnessType[])); // 改为float[]以支持加权值

            foreach (var kvp in userVectors)
            {
                DataRow row = result.NewRow();
                row["USER_ID"] = kvp.Key;
                row["CATEGORY_VECTOR"] = kvp.Value;
                result.Rows.Add(row);
            }

            return result;
        }

        public List<BorrowList> History(UserType userID)
        {
            var borrowTable = _repository.GetMergedBookRecords();
            // 只记录一定时期内观看历史，一定无需 Trigger
            var borrowHistoryTable = GetUserBorrowHistory(borrowTable, userID);
            var borrowHistoryList = DataTableConverter.ConvertToBorrowList(borrowHistoryTable);
            foreach (var book in borrowHistoryList)
                book.CoverUrl = $"/api/bookcover/{book.BookID}";

            return borrowHistoryList;
        }

        public Dictionary<CateNameType, Int64> Interest(UserType userID)
        {
            // TODO 后期可以利用 Trigger 更新，进行查询
            // TODO 阅读兴趣随时间变化权重考虑。
            var borrowTable = _repository.GetMergedBookRecords();
            var categoryTable = _repository.GetCategoryTable();
            var userCategoryVector = GetUserCategoryVector(borrowTable);
            var rows = userCategoryVector.AsEnumerable()
                        .Where(row => row.Field<UserType>("USER_ID") == userID)
                        .ToList();

            // 用户未曾借书或无相应用户
            if (rows.Count == 0)
            {
                //throw new ArgumentException($"No user found with ID {userID}");
                Console.WriteLine($"No user found with ID {userID}");
                return new Dictionary<CateNameType, Int64>();
            }
            // 数据库错误
            if (rows.Count > 1)
            {
                //throw new InvalidOperationException($"Multiple users found with ID {userID}");
                Console.WriteLine($"Multiple users found with ID {userID}");
                return new Dictionary<CateNameType, Int64>();
            }

            var categoryVector = rows[0].Field<Int64[]>("CATEGORY_VECTOR");

            // 用户未曾借书
            if (categoryVector == null)
            {
                //throw new InvalidOperationException($"CATEGORY_VECTOR is null for user ID {userID}");
                Console.WriteLine($"CATEGORY_VECTOR is null for user ID {userID}");
                return new Dictionary<CateNameType, Int64>();
            }

            Dictionary<CateNameType, Int64> interestDict =
                categoryTable.AsEnumerable().ToDictionary(
                    row => row.Field<CateNameType>("CATEGORY_NAME"),
                    row => 
                    {
                        var index = row.Field<CategoryType>("CATEGORY_ID");
                        return index < categoryVector.Length ? categoryVector[index] : 0;
                    }
                );

            return interestDict;
        }
    }
}
