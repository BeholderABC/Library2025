using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.VisualBasic;
using Oracle.ManagedDataAccess.Client;
using System.Collections.Generic;
using WebLibrary.Pages.Shared.Models;

namespace WebLibrary.Pages.Admin
{
    public class DisplayTableModel : PageModel
    {
        private readonly IConfiguration _config;

        public DisplayTableModel(IConfiguration config)
        {
            _config = config;
        }

        public List<string> ColumnNames { get; set; } = new();  // 存储列名
        public List<List<object>> TableData { get; set; } = new();  // 存储表数据
        public string SortColumn { get; set; } = "";  // 当前排序的列
        public string SortOrder { get; set; } = "ASC";  // 当前排序的顺序，默认为升序

        public void OnGet(string sortColumn, string sortOrder)
        {
            // 确保排序列合法
            var validColumns = new List<string> { "RECORD_ID", "USER_ID", "COPY_ID", "BORROW_DATE", "DUE_DATE", "STATUS", "RENEW_TIMES", "RETURN_DATE", "LAST_FINED_DATE"
                                    };  // 这里列出你的合法列名

            if (!validColumns.Contains(sortColumn)) sortColumn = "RECORD_ID";  // 默认排序列
            // 确保排序顺序合法
            if (sortOrder != "ASC" && sortOrder != "DESC") sortOrder = "ASC";  // 默认升序
            // 打印调试信息

            SortColumn = (sortColumn ?? "RECORD_ID").ToUpperInvariant();
            SortOrder = (sortOrder ?? "ASC").ToUpperInvariant();

            Console.WriteLine($"Sorting by column: {sortColumn}, Order: {sortOrder}");


            // 连接数据库
            using var conn = new OracleConnection(_config.GetConnectionString("OracleDb"));
            conn.Open();

            // 获取列名
            var columnQuery = "SELECT column_name FROM user_tab_columns WHERE table_name = 'BORROWRECORD'";
            using var cmd = new OracleCommand(columnQuery, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                ColumnNames.Add(reader.GetString(0));
            }

            // 获取表数据并按照排序列和排序顺序排序
            var dataQuery = $"SELECT * FROM BORROWRECORD ORDER BY {SortColumn} {SortOrder}";  // 动态排序查询
            using var dataCmd = new OracleCommand(dataQuery, conn);
            using var dataReader = dataCmd.ExecuteReader();

            while (dataReader.Read())
            {
                var rowData = new List<object>();
                foreach (var columnName in ColumnNames)
                {
                    rowData.Add(dataReader[columnName]);  // 按列名获取数据
                }
                TableData.Add(rowData);  // 将数据行加入结果列表
            }
        }

    }
}
