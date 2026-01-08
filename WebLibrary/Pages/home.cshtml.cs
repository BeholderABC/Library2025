using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Oracle.ManagedDataAccess.Client;
using System.ComponentModel.DataAnnotations;
using System.Data;
using Scroll.Models;
using Scroll.Services;
using System.Security.Claims;

namespace WebLibrary.Pages
{
    public class homeModel : PageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<homeModel> _logger;

        private ICurated _curated;

        public homeModel(IConfiguration config, ILogger<homeModel> logger, ICurated curated)
        {
            _config = config;
            _logger = logger;

            _curated = curated;
        }

        public List<BookInfo> HotBooks { get; set; } = new List<BookInfo>();
        public Dictionary<string, List<BookInfo>> CategoryHotBooks { get; set; } = new Dictionary<string, List<BookInfo>>();

        public List<BookList>? RecommendedBooks { get; set; }
        
        // 讲座相关数据
        public List<LectureInfo> Lectures { get; set; } = new List<LectureInfo>();
        public List<LectureInfo> UpcomingLectures { get; set; } = new List<LectureInfo>();
        public Dictionary<int, bool> LectureDates { get; set; } = new Dictionary<int, bool>();

        // 通知相关数据
        public List<NotificationInfo> RecentNotifications { get; set; } = new List<NotificationInfo>();

        public class BookInfo
        {
            public int BookId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string? Author { get; set; }
            public string? ISBN { get; set; }
            public string? Publisher { get; set; }
            public DateTime? PublicationDate { get; set; }
            public string? Description { get; set; }
            public int CategoryId { get; set; }
            public string? CategoryName { get; set; }

            [DisplayFormat(DataFormatString = "{0:0.0}")]
            public decimal BookRating { get; set; }

            [Display(Name = "借阅次数")]
            public int BorrowCount { get; set; }
        }

        public class LectureInfo
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Speaker { get; set; } = string.Empty;
            public DateTime LectureDate { get; set; }
            public string? Summary { get; set; }
            public byte[]? Picture { get; set; }
            public int MaxNum { get; set; }
            public int NowNum { get; set; }
        }

        public class NotificationInfo
        {
            public long Id { get; set; }
            public string Content { get; set; } = string.Empty;
            public DateTime CreateTime { get; set; }
            public bool IsRead { get; set; }
            public string SenderId { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            try
            {
                using (var conn = new OracleConnection(_config.GetConnectionString("OracleDb")))
                {
                    conn.Open();

                    // 获取全站热门图书
                    using (var cmd = new OracleCommand())
                    {
                        cmd.Connection = conn;
                        cmd.BindByName = true;
                        cmd.CommandText = @"
                            SELECT b.book_id, b.title, b.author, b.ISBN, b.publisher, 
                                   b.publication_date, b.description, b.book_rating, 
                                   b.borrow_count, b.category_id, c.category_name
                            FROM book b
                            JOIN category c ON b.category_id = c.category_id
                            WHERE b.book_rating >= 3
                            ORDER BY b.borrow_count DESC";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var book = new BookInfo
                                {
                                    BookId = Convert.ToInt32(reader["book_id"]),
                                    Title = reader["title"].ToString() ?? string.Empty,
                                    Author = reader["author"] as string,
                                    ISBN = reader["ISBN"] as string,
                                    Publisher = reader["publisher"] as string,
                                    PublicationDate = reader["publication_date"] as DateTime?,
                                    Description = reader["description"] as string,
                                    BookRating = Convert.ToDecimal(reader["book_rating"]),
                                    BorrowCount = Convert.ToInt32(reader["borrow_count"]),
                                    CategoryId = Convert.ToInt32(reader["category_id"]),
                                    CategoryName = reader["category_name"].ToString()
                                };
                                HotBooks.Add(book);
                            }
                        }
                    }

                    // 获取每个分类的热门图书
                    using (var cmd = new OracleCommand())
                    {
                        cmd.Connection = conn;
                        cmd.BindByName = true;
                        cmd.CommandText = @"
                            WITH ranked_books AS (
                                SELECT 
                                    b.book_id, b.title, b.author, b.ISBN, b.publisher, 
                                    b.publication_date, b.description, b.book_rating, 
                                    b.borrow_count, b.category_id, c.category_name,
                                    ROW_NUMBER() OVER (PARTITION BY b.category_id ORDER BY b.borrow_count DESC) as rank
                                FROM book b
                                JOIN category c ON b.category_id = c.category_id
                                WHERE b.book_rating >= 3
                            )
                            SELECT * FROM ranked_books WHERE rank <= 3";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var book = new BookInfo
                                {
                                    BookId = Convert.ToInt32(reader["book_id"]),
                                    Title = reader["title"].ToString() ?? string.Empty,
                                    Author = reader["author"] as string,
                                    ISBN = reader["ISBN"] as string,
                                    Publisher = reader["publisher"] as string,
                                    PublicationDate = reader["publication_date"] as DateTime?,
                                    Description = reader["description"] as string,
                                    BookRating = Convert.ToDecimal(reader["book_rating"]),
                                    BorrowCount = Convert.ToInt32(reader["borrow_count"]),
                                    CategoryId = Convert.ToInt32(reader["category_id"]),
                                    CategoryName = reader["category_name"].ToString()
                                };

                                if (!CategoryHotBooks.ContainsKey(book.CategoryName ?? string.Empty))
                                {
                                    CategoryHotBooks[book.CategoryName ?? string.Empty] = new List<BookInfo>();
                                }
                                CategoryHotBooks[book.CategoryName ?? string.Empty].Add(book);
                            }
                        }
                    }

                    // 推荐图书
                    if (int.TryParse(User.FindFirst("UserId")?.Value, out var uid))
                        RecommendedBooks = _curated.CuratedList(uid);

                    // 获取最近通知
                    await LoadRecentNotificationsAsync(conn);

                    // 获取讲座数据
                    try
                    {
                        // 检查LECTURE表是否存在
                        using (var cmd = new OracleCommand())
                        {
                            cmd.Connection = conn;
                            cmd.BindByName = true;
                            cmd.CommandText = "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'LECTURE'";
                            
                            var tableExists = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                            if (tableExists == 0)
                            {
                                _logger.LogInformation("LECTURE表不存在，跳过讲座数据加载");
                            }
                            else
                            {
                                // 获取所有讲座数据
                                using (var lectureCmd = new OracleCommand())
                                {
                                    lectureCmd.Connection = conn;
                                    lectureCmd.BindByName = true;
                                    lectureCmd.CommandText = @"
                                        SELECT ID, NAME, LECTURE_DATE, SPEAKER, SUMMARY, PICTURE, MAX_NUM, NOW_NUM 
                                        FROM LECTURE 
                                        ORDER BY LECTURE_DATE DESC";

                                    using (var reader = await lectureCmd.ExecuteReaderAsync())
                                    {
                                        while (await reader.ReadAsync())
                                        {
                                            var lecture = new LectureInfo
                                            {
                                                Id = Convert.ToInt32(reader["ID"]),
                                                Name = reader["NAME"].ToString() ?? string.Empty,
                                                Speaker = reader["SPEAKER"].ToString() ?? string.Empty,
                                                LectureDate = Convert.ToDateTime(reader["LECTURE_DATE"]),
                                                Summary = reader["SUMMARY"] as string,
                                                MaxNum = Convert.ToInt32(reader["MAX_NUM"]),
                                                NowNum = Convert.ToInt32(reader["NOW_NUM"])
                                            };

                                            if (reader["PICTURE"] != DBNull.Value)
                                            {
                                                lecture.Picture = (byte[])reader["PICTURE"];
                                            }

                                            Lectures.Add(lecture);
                                        }
                                    }
                                }

                                // 获取未来最近的三个讲座
                                var now = DateTime.Now;
                                UpcomingLectures = Lectures
                                    .Where(l => l.LectureDate > now)
                                    .OrderBy(l => l.LectureDate)
                                    .Take(3)
                                    .ToList();

                                // 构建当月讲座日期字典
                                var currentMonth = DateTime.Now;
                                var daysInMonth = DateTime.DaysInMonth(currentMonth.Year, currentMonth.Month);
                                
                                for (int day = 1; day <= daysInMonth; day++)
                                {
                                    var date = new DateTime(currentMonth.Year, currentMonth.Month, day);
                                    var hasLecture = Lectures.Any(l => l.LectureDate.Date == date.Date);
                                    LectureDates[day] = hasLecture;
                                }

                                _logger.LogInformation("成功加载讲座数据，共 {Count} 个讲座，未来讲座 {UpcomingCount} 个",
                                    Lectures.Count, UpcomingLectures.Count);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "加载讲座数据时发生错误");
                    }
                }

                _logger.LogInformation("成功获取热门图书列表，全站热门 {Count} 本，分类热门 {CategoryCount} 类",
                    HotBooks.Count, CategoryHotBooks.Count);
            }
            catch (OracleException ex)
            {
                _logger.LogError(ex, "Oracle数据库错误，错误代码: {ErrorCode}", ex.Number);
                ModelState.AddModelError("", $"数据库错误(ORA-{ex.Number}): {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取热门图书列表时发生错误");
                ModelState.AddModelError("", "获取热门图书列表时出错: " + ex.Message);
            }
        }

        private async Task LoadRecentNotificationsAsync(OracleConnection conn)
        {
            try
            {
                var userId = User.FindFirst("UserId")?.Value;
                var roleCn = User.FindFirst(ClaimTypes.Role)?.Value;
                
                if (userId == null || roleCn == null)
                {
                    _logger.LogInformation("用户未登录，跳过通知加载");
                    return;
                }

                // 根据角色选择对应的通知表
                string table = roleCn switch
                {
                    "学生" => "NOTIFICATIONS_STUDENT",
                    "图书馆管理员" => "NOTIFICATIONS_LIBRARIAN",
                    "其他教职工" => "NOTIFICATIONS_STAFF",
                    _ => null
                };

                if (table == null)
                {
                    _logger.LogWarning("未知的用户角色: {Role}", roleCn);
                    return;
                }

                using (var cmd = new OracleCommand())
                {
                    cmd.Connection = conn;
                    cmd.BindByName = true;
                    cmd.CommandText = $@"
                        SELECT ID, CONTENT, CREATE_TIME, IS_READ, SENDER_ID
                        FROM {table}
                        WHERE USER_ID = :userId
                        ORDER BY CREATE_TIME DESC
                        FETCH FIRST 3 ROWS ONLY";

                    cmd.Parameters.Add("userId", userId);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var notification = new NotificationInfo
                            {
                                Id = Convert.ToInt64(reader["ID"]),
                                Content = reader["CONTENT"].ToString() ?? string.Empty,
                                CreateTime = Convert.ToDateTime(reader["CREATE_TIME"]),
                                IsRead = Convert.ToInt32(reader["IS_READ"]) == 1,
                                SenderId = reader["SENDER_ID"]?.ToString() ?? string.Empty
                            };
                            RecentNotifications.Add(notification);
                        }
                    }
                }

                _logger.LogInformation("成功加载用户 {UserId} 的最近通知，共 {Count} 条", userId, RecentNotifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载最近通知时发生错误");
            }
        }
    }
}
