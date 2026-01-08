using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System;
using System.Data;
using System.Linq;
//using Scroll.Models.Entities;

namespace Scroll.Database
{
    using BookType = Int64;
    using UserType = Decimal;

    public class ScrollDB : IScrollDB
    {
        private readonly DatabaseContext _context;

        public ScrollDB(DatabaseContext context)
        {
            _context = context;
        }
        
        public DataTable GetMergedBookRecords()
        {
            return GetMergedBookRecords(DateTime.MinValue, DateTime.Now);
        }
        // 以下是需求函数
        public DataTable GetMergedBookRecords(DateTime startDate, DateTime endDate)
        {
            string sql = @"
                SELECT 
                    BR.USER_ID, 
                    BR.BORROW_DATE,
                    BR.DUE_DATE,
                    BR.RETURN_DATE,
                    BR.STATUS,
                    B.BOOK_ID,
                    B.TITLE,
                    B.AUTHOR,
                    B.BOOK_RATING,
                    B.CATEGORY_ID,
                    CT.CATEGORY_NAME
                FROM 
                    BORROWRECORD BR
                JOIN 
                    COPY C ON BR.COPY_ID = C.COPY_ID
                JOIN 
                    BOOK B ON C.BOOK_ID = B.BOOK_ID
                JOIN 
                    CATEGORY CT ON B.CATEGORY_ID = CT.CATEGORY_ID
                WHERE
                    BR.BORROW_DATE BETWEEN :startDate AND :endDate
            ";

            var connection = _context.GetConnection();
            using (var cmd = new OracleCommand(sql, connection))
            using (var adapter = new OracleDataAdapter(cmd))
            {
                cmd.Parameters.Add(new OracleParameter("startDate", OracleDbType.Date)).Value = startDate;
                cmd.Parameters.Add(new OracleParameter("endDate", OracleDbType.Date)).Value = endDate;

                var table = new DataTable();
                adapter.Fill(table);

                foreach (DataRow row in table.Rows)
                    if (row.IsNull("BOOK_RATING"))
                        row["BOOK_RATING"] = 0.0;


                return table;
            }
        }
        public DataTable GetCategoryTable()
        {
            string sql = @"
                SELECT 
                    CATEGORY_ID,
                    CATEGORY_NAME
                FROM 
                    CATEGORY
            ";

            var connection = _context.GetConnection();
            using (var cmd = new OracleCommand(sql, connection))
            using (var adapter = new OracleDataAdapter(cmd))
            {
                var table = new DataTable();
                adapter.Fill(table);
                return table;
            }
        }

        public async Task<byte[]?> GetCoverAsync(BookType bookID)
        {
            string sql = @"
                SELECT COVER 
                FROM BOOK_COVER 
                WHERE BOOK_ID = :id
            ";
            var connection = await _context.GetConnectionAsync();

            using var cmd = new OracleCommand(sql, connection);
            cmd.Parameters.Add(new OracleParameter("id", bookID));
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync() && !reader.IsDBNull(0))
                return (byte[])reader["COVER"];
            return null;
        }
        
        public void ShrinkAvatar()
        {
            var connection = _context.GetConnection();

            // Step 1: Enable ROW MOVEMENT (DDL)
            string enableMovementSql = "ALTER TABLE USER_AVATAR ENABLE ROW MOVEMENT";
            using (OracleCommand enableCommand = new OracleCommand(enableMovementSql, connection))
            {
                enableCommand.ExecuteNonQuery();
                Console.WriteLine("ROW MOVEMENT enabled on USER_AVATAR.");
            }

            // Step 2: Shrink SPACE (DDL)
            string shrinkSql = "ALTER TABLE USER_AVATAR SHRINK SPACE CASCADE";
            using (OracleCommand shrinkCommand = new OracleCommand(shrinkSql, connection))
            {
                shrinkCommand.ExecuteNonQuery();
                Console.WriteLine("Table USER_AVATAR space has been shrunk.");
            }

            // Step 3: Disable ROW MOVEMENT
            string disableMovementSql = "ALTER TABLE USER_AVATAR DISABLE ROW MOVEMENT";
            using (OracleCommand disableCommand = new OracleCommand(disableMovementSql, connection))
            {
                disableCommand.ExecuteNonQuery();
                Console.WriteLine("ROW MOVEMENT disabled on USER_AVATAR.");
            }
        }

        public async Task<bool> ChangeAvatarAsync(UserType userId, byte[] imageBytes)
        {
            //string sql = @"
            //    MERGE INTO USER_AVATAR ua
            //    USING (SELECT :id AS USER_ID, :img AS AVATAR FROM dual) src
            //    ON (ua.USER_ID = src.USER_ID)
            //    WHEN MATCHED THEN
            //      UPDATE SET ua.AVATAR = src.AVATAR
            //    WHEN NOT MATCHED THEN
            //      INSERT (USER_ID, AVATAR)
            //      VALUES (src.USER_ID, src.AVATAR);
            //";
            if (imageBytes == null || imageBytes.Length == 0)
                return false;

            var connection = await _context.GetConnectionAsync();

            // Step 1: Execute DELETE command (DML)
            string deleteSql = "DELETE FROM USER_AVATAR WHERE USER_ID = :id";
            using (var deleteCommand = new OracleCommand(deleteSql, connection))
            {
                deleteCommand.Parameters.Add(new OracleParameter("id", userId));
                await deleteCommand.ExecuteNonQueryAsync();
            }
    
            // Step 2: Execute INSERT command (DML)
            string insertSql = @"INSERT INTO USER_AVATAR (USER_ID, AVATAR) VALUES (:id, :img)";
            using (var insertCommand = new OracleCommand(insertSql, connection))
            {
                insertCommand.CommandText = insertSql;
                insertCommand.Parameters.Add(":id", OracleDbType.Decimal).Value = userId;
                insertCommand.Parameters.Add(":img", OracleDbType.Blob).Value = imageBytes;
                int rowsAffected = await insertCommand.ExecuteNonQueryAsync();
                
                Console.WriteLine("Rows changed from USER_AVATAR.");
                return rowsAffected > 0;
            }
        }

        public async Task<byte[]?> GetAvatarAsync(UserType userID)
        {
            string sql = @"
                SELECT AVATAR 
                FROM USER_AVATAR 
                WHERE USER_ID = :id
            ";
            var connection = await _context.GetConnectionAsync();

            using var cmd = new OracleCommand(sql, connection);
            cmd.Parameters.Add(new OracleParameter("id", userID));
            using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync() && !reader.IsDBNull(0))
                return (byte[])reader["AVATAR"];
            return null;
        }

        // 以下是显示数据库表信息的函数
        public DataTable GetTableData(string tableName)
        {
            var dataTable = new DataTable();
            string sql = $"SELECT * FROM {tableName}";
            var connection = _context.GetConnection();

            using (var cmd = new OracleCommand(sql, connection))
            using (var adapter = new OracleDataAdapter(cmd))
            {
                adapter.Fill(dataTable);
            }
            return dataTable;
        }

        public void ShowAllTables()
        {
            string sql = @"SELECT table_name FROM user_tables ORDER BY table_name";
            var connection = _context.GetConnection();

            using (var cmdTables = new OracleCommand(sql, connection))
            using (var reader = cmdTables.ExecuteReader())
            {
                while (reader.Read())
                {
                    string tableName = reader.GetString(0);
                    Console.WriteLine($"表：{tableName}");

                    ShowTableColumns(tableName, connection);
                    Console.WriteLine();
                }
            }
        }

        private void ShowTableColumns(string tableName, OracleConnection connection)
        {
            string columnSql = $"SELECT column_name, data_type, data_length, nullable FROM user_tab_columns WHERE table_name = '{tableName}'";
            using (var cmdColumns = new OracleCommand(columnSql, connection))
            using (var columnReader = cmdColumns.ExecuteReader())
            {
                while (columnReader.Read())
                {
                    string col = columnReader.GetString(0);
                    string type = columnReader.GetString(1);
                    int len = columnReader.GetInt32(2);
                    string nullable = columnReader.GetString(3);
                    Console.WriteLine($"\t字段: {col,-20} 类型: {type,-10} 长度: {len,-4} 可空: {nullable}");
                }
            }
        }

        public void ShowAllForeignKeys()
        {
            string sql = @"
                SELECT 
                    a.owner AS schema_name,
                    a.table_name,
                    a.constraint_name,
                    a.r_owner AS referenced_schema,
                    a.r_constraint_name AS referenced_pk,
                    b.table_name AS referenced_table,
                    c.column_name,
                    d.column_name AS referenced_column,
                    a.delete_rule
                FROM 
                    all_constraints a
                JOIN 
                    all_constraints b ON a.r_constraint_name = b.constraint_name AND a.r_owner = b.owner
                JOIN 
                    all_cons_columns c ON a.constraint_name = c.constraint_name AND a.owner = c.owner
                JOIN 
                    all_cons_columns d ON b.constraint_name = d.constraint_name AND b.owner = d.owner
                WHERE 
                    a.constraint_type = 'R' AND a.owner = 'ADMINISTRATOR'
                ORDER BY 
                    a.owner, a.table_name, a.constraint_name
            ";

            var connection = _context.GetConnection();
            using (var command = new OracleCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                Console.WriteLine("\n外键约束信息:");
                Console.WriteLine("--------------------------------------------------------------------------------------------------");
                Console.WriteLine("| 架构名 | 表名 | 外键名 | 引用架构 | 引用主键 | 引用表 | 列名 | 引用列 | 删除规则 |");
                Console.WriteLine("--------------------------------------------------------------------------------------------------");

                while (reader.Read())
                {
                    Console.WriteLine($"| {reader["schema_name"],-6} | {reader["table_name"],-15} | {reader["constraint_name"],-20} | " +
                        $"{reader["referenced_schema"],-8} | {reader["referenced_pk"],-15} | {reader["referenced_table"],-15} | " +
                        $"{reader["column_name"],-10} | {reader["referenced_column"],-10} | {reader["delete_rule"],-10} |");
                }

                Console.WriteLine("--------------------------------------------------------------------------------------------------");
            }
        }

        // 以下是会对数据库进行修改的函数
        private void CreateCoverTable()
        {
            var connection = _context.GetConnection();
            string createTableSql = @"
                CREATE TABLE BOOK_COVER (
                    BOOK_ID NUMBER PRIMARY KEY,
                    COVER BLOB,
                    CONSTRAINT FK_BOOK_COVER 
                        FOREIGN KEY (BOOK_ID) 
                        REFERENCES BOOK(BOOK_ID)
                )";

            using (OracleCommand command = new OracleCommand(createTableSql, connection))
            {
                command.ExecuteNonQuery();
                Console.WriteLine("Table created successfully.");
            }
        }

        private void AddCover(BookType bookID, string imagePath)
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            var connection = _context.GetConnection();
            string sql = @"
                INSERT INTO BOOK_COVER (BOOK_ID, COVER) VALUES (:id, :img)
            ";

            using (var cmd = new OracleCommand(sql, connection))
            {
                cmd.Parameters.Add(":id", OracleDbType.Decimal).Value = bookID;
                cmd.Parameters.Add(":img", OracleDbType.Blob).Value = imageBytes;

                cmd.ExecuteNonQuery();
                Console.WriteLine($"已为 {bookID} 插入封面！");
            }
        }

        // 从数据库获取图片并保存到本地文件
        public void DownloadCover(BookType bookId, string outputFileDir)
        {
            var connection = _context.GetConnection();
            string sql = @"
                SELECT COVER 
                FROM BOOK_COVER 
                WHERE BOOK_ID = :id
            ";
            var outputFilePath = @$"{outputFileDir}\{bookId}.jpg";

            using (var cmd = new OracleCommand(sql, connection))
            {
                cmd.Parameters.Add(":id", OracleDbType.Decimal).Value = bookId;

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        // 读取BLOB数据
                        byte[] imageBytes = (byte[])reader["COVER"];

                        // 将字节数组写入文件
                        File.WriteAllBytes(outputFilePath, imageBytes);
                        Console.WriteLine($"已成功下载 {bookId} 的封面到 {outputFilePath}");
                    }
                    else
                    {
                        Console.WriteLine($"未找到 {bookId} 的封面");
                    }
                }
            }
        }

        private void CreateAvatarTable(OracleConnection conn)
        {
            string createTableSql = @"
                CREATE TABLE USER_AVATAR (
                    USER_ID NUMBER PRIMARY KEY,
                    AVATAR BLOB,
                    CONSTRAINT FK_USER_AVATAR 
                        FOREIGN KEY (USER_ID) 
                        REFERENCES USERS(USER_ID)
                )";
            using (OracleCommand command = new OracleCommand(createTableSql, conn))
            {
                command.ExecuteNonQuery();
                Console.WriteLine("Table created successfully.");
            }
        }



        public static void AddAvatar(OracleConnection conn, UserType userId, string imagePath)
        {
            byte[] imageBytes = File.ReadAllBytes(imagePath);
            string sql = @"INSERT INTO USER_AVATAR (USER_ID, AVATAR) VALUES (:id, :img)";
            //string sql = @"
            //    UPDATE USER_AVATAR
            //    SET AVATAR = :img
            //    WHERE USER_ID = :id
            //";

            using (var cmd = new OracleCommand(sql, conn))
            {
                cmd.Parameters.Add(":id", OracleDbType.Decimal).Value = userId;

                OracleParameter imgParam = new OracleParameter();
                imgParam.ParameterName = ":img";
                imgParam.OracleDbType = OracleDbType.Blob;
                imgParam.Value = imageBytes;
                cmd.Parameters.Add(imgParam);

                cmd.ExecuteNonQuery();
                Console.WriteLine($"已为 {userId} 更换头像！");
            }
        }
    }
}
