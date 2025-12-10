using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;

namespace MssqlMcpServer.Infrastructure
{
    public static class RunSelectHelper
    {
        /// <summary>
        /// แปลงค่า (รวมถึง JsonElement) ให้เป็นชนิด .NET ที่ SqlClient รองรับ
        /// คืน DbType สำหรับ Dapper ไปด้วย
        /// - Object/Array: serialize เป็น string (NVARCHAR)
        /// - Null: DBNull.Value
        /// - Number: พยายาม map เป็น Int64 ก่อน, ถ้าไม่ลงตัวใช้ Decimal, ไม่งั้น Double
        /// - String ที่เป็น Guid/DateTime/DateTimeOffset: จับชนิดให้ตรง
        /// </summary>
        public static object? NormalizeParamValue(object? value, out DbType dbType)
        {
            if (value is null)
            {
                dbType = DbType.Object;
                return DBNull.Value;
            }

            if (value is JsonElement je)
            {
                switch (je.ValueKind)
                {
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        dbType = DbType.Object;
                        return DBNull.Value;

                    case JsonValueKind.String:
                        var s = je.GetString() ?? string.Empty;

                        // ลองจับเป็น Guid/DateTime/DateTimeOffset ก่อน
                        if (Guid.TryParse(s, out var g))
                        {
                            dbType = DbType.Guid;
                            return g;
                        }
                        if (DateTimeOffset.TryParse(s, out var dto))
                        {
                            dbType = DbType.DateTimeOffset;
                            return dto;
                        }
                        if (DateTime.TryParse(s, out var dt))
                        {
                            dbType = DbType.DateTime;
                            return dt;
                        }

                        dbType = DbType.String;
                        return s;

                    case JsonValueKind.Number:
                        // ลองเป็น long ก่อน
                        if (je.TryGetInt64(out var l))
                        {
                            dbType = DbType.Int64;
                            return l;
                        }
                        // ลองเป็น decimal
                        if (je.TryGetDecimal(out var dec))
                        {
                            dbType = DbType.Decimal;
                            return dec;
                        }
                        // สุดท้าย double
                        dbType = DbType.Double;
                        return je.GetDouble();

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        dbType = DbType.Boolean;
                        return je.GetBoolean();

                    case JsonValueKind.Object:
                    case JsonValueKind.Array:
                        // เก็บเป็น JSON string
                        var json = je.GetRawText();
                        dbType = DbType.String;
                        return json;

                    default:
                        dbType = DbType.String;
                        return je.ToString();
                }
            }

            // ถ้าไม่ใช่ JsonElement — map ชนิดมาตรฐาน
            switch (value)
            {
                case string s:
                    if (Guid.TryParse(s, out var g))
                    {
                        dbType = DbType.Guid; return g;
                    }
                    if (DateTimeOffset.TryParse(s, out var dto))
                    {
                        dbType = DbType.DateTimeOffset; return dto;
                    }
                    if (DateTime.TryParse(s, out var dt))
                    {
                        dbType = DbType.DateTime; return dt;
                    }
                    dbType = DbType.String; return s;

                case bool _: dbType = DbType.Boolean; return value;
                case byte _: dbType = DbType.Byte; return value;
                case short _: dbType = DbType.Int16; return value;
                case int _: dbType = DbType.Int32; return value;
                case long _: dbType = DbType.Int64; return value;
                case float _: dbType = DbType.Single; return value;
                case double _: dbType = DbType.Double; return value;
                case decimal _: dbType = DbType.Decimal; return value;
                case DateTime _: dbType = DbType.DateTime; return value;
                case DateTimeOffset _: dbType = DbType.DateTimeOffset; return value;
                case Guid _: dbType = DbType.Guid; return value;
                case byte[] _: dbType = DbType.Binary; return value;

                default:
                    // เผื่อเคส POCO/Dictionary/Array อื่น ๆ => serialize เป็น JSON string
                    var json = JsonSerializer.Serialize(value);
                    dbType = DbType.String;
                    return json;
            }
        }
    }
}
