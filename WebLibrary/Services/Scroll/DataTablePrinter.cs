using System;
using System.Data;
using System.Linq;

namespace Scroll.Utilities
{
    public static class DataTablePrinter
    {
        public static void PrintDataTable(DataTable dt)
        {
            // 获取非BLOB列的列表
            var nonBlobColumns = dt.Columns.Cast<DataColumn>()
                .Where(c => c.DataType != typeof(byte[]))
                .ToList();

            // 计算每列的最大宽度（考虑中英文字符宽度）
            var columnWidths = nonBlobColumns
                .Select(c => new {
                    Column = c,
                    Width = Math.Max(
                        dt.Rows.Cast<DataRow>()
                            .Select(r => GetDisplayWidth(FormatValue(r[c])))
                            .DefaultIfEmpty(0).Max(),
                        GetDisplayWidth($"{c.ColumnName} ({GetTypeName(c.DataType)})"))
                })
                .ToDictionary(x => x.Column, x => x.Width + 2);

            // 打印表头
            PrintHeader(dt, columnWidths);
            
            // 打印分隔线
            PrintSeparator(dt, columnWidths);
            
            // 打印数据行
            PrintDataRows(dt, columnWidths);
        }

        private static void PrintHeader(DataTable dt, Dictionary<DataColumn, int> columnWidths)
        {
            foreach (DataColumn column in dt.Columns)
            {
                string header = $"{column.ColumnName} ({GetTypeName(column.DataType)})";
                if (column.DataType == typeof(byte[]))
                {
                    Console.Write(header);
                }
                else
                {
                    Console.Write(PadRightMixed(header, columnWidths[column]));
                }
            }
            Console.WriteLine();
        }

        private static void PrintSeparator(DataTable dt, Dictionary<DataColumn, int> columnWidths)
        {
            foreach (DataColumn column in dt.Columns)
            {
                if (column.DataType == typeof(byte[]))
                {
                    string header = $"{column.ColumnName} ({GetTypeName(column.DataType)})";
                    Console.Write(new string('-', header.Length));
                }
                else
                {
                    Console.Write(new string('-', columnWidths[column]));
                }
            }
            Console.WriteLine();
        }

        private static void PrintDataRows(DataTable dt, Dictionary<DataColumn, int> columnWidths)
        {
            foreach (DataRow row in dt.Rows)
            {
                foreach (DataColumn column in dt.Columns)
                {
                    if (column.DataType == typeof(byte[]))
                    {
                        Console.Write("<BLOB>");
                    }
                    else
                    {
                        string value = FormatValue(row[column]);
                        Console.Write(PadRightMixed(value, columnWidths[column]));
                    }
                }
                Console.WriteLine();
            }
        }

        private static int GetDisplayWidth(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return s.Sum(c => c > 255 ? 2 : 1);
        }

        private static string PadRightMixed(string s, int totalWidth)
        {
            if (string.IsNullOrEmpty(s)) return s;

            int currentWidth = GetDisplayWidth(s);
            if (currentWidth >= totalWidth) return s;

            return s + new string(' ', totalWidth - currentWidth);
        }

        private static string FormatValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "NULL";
            }

            if (value.GetType().IsArray && value.GetType() != typeof(byte[]))
            {
                var array = value as Array;
                return $"[{string.Join(", ", array.Cast<object>())}]";
            }

            return value.ToString();
        }

        private static string GetTypeName(Type type)
        {
            if (type == typeof(byte[]))
            {
                return "BLOB";
            }
            if (type.IsArray)
            {
                return $"{type.GetElementType().Name}[]";
            }
            return type.Name;
        }
    }
}
