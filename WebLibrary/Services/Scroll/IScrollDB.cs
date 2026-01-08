using System.Data;

namespace Scroll.Database
{
    using BookType = Int64;
    using UserType = Decimal;
    public interface IScrollDB
    {
        DataTable GetMergedBookRecords();
        DataTable GetMergedBookRecords(DateTime startDate, DateTime endDate);
        DataTable GetCategoryTable();
        Task<byte[]?> GetCoverAsync(BookType bookID);
        Task<byte[]?> GetAvatarAsync(UserType userID);
        Task<bool> ChangeAvatarAsync(UserType userID, byte[] imageBytes);
        //bool ChangeAvatar(UserType userID, byte[] imageBytes);

        //DataTable GetTableData(string tableName);
        //void ShowAllTables();
        //void ShowAllForeignKeys();
    }
}