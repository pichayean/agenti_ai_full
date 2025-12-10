using Microsoft.Data.SqlClient;

namespace MssqlMcpServer.Common
{
    public static class ConnectionStringMasker
    {
        /// <summary>
        /// ปิดบังค่า Database, User Id, Password ใน connection string
        /// </summary>
        /// <param name="connectionString">ต้นฉบับ</param>
        /// <param name="maskChar">อักขระที่ใช้ปิดบัง (เช่น • หรือ *)</param>
        /// <param name="preserveLength">ถ้าจริง จะคงความยาวเดิมของค่าไว้ (เพื่อความสวยงาม/ดีบัก)</param>
        public static string MaskSensitive(string connectionString, char maskChar = '•', bool preserveLength = true)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return connectionString ?? string.Empty;

            // ใช้ตัวพาร์สทางการของ .NET เพื่อกันเคสคีย์หลายรูปแบบ (Server/Data Source, Database/Initial Catalog ฯลฯ)
            var sb = new SqlConnectionStringBuilder(connectionString);

            // Database (Initial Catalog)
            if (!string.IsNullOrEmpty(sb.InitialCatalog))
                sb.InitialCatalog = MaskValue(sb.InitialCatalog, maskChar, preserveLength);

            // User Id
            if (!string.IsNullOrEmpty(sb.UserID))
                sb.UserID = MaskValue(sb.UserID, maskChar, preserveLength);

            // Password
            if (!string.IsNullOrEmpty(sb.Password))
                sb.Password = MaskValue(sb.Password, maskChar, preserveLength);

            return sb.ConnectionString;
        }

        private static string MaskValue(string value, char maskChar, bool preserveLength)
        {
            if (string.IsNullOrEmpty(value)) return value;

            if (preserveLength)
                return new string(maskChar, value.Length);

            // อย่างน้อยให้เห็นความยาว 8 ตัว เพื่อไม่หลุดความลับเรื่องความยาวเกินไป
            int len = Math.Max(8, Math.Min(value.Length, 64));
            return new string(maskChar, len);
        }
    }
}
