// using System;
// using System.Collections.Generic;
// using System.ComponentModel;
// using System.Threading.Tasks;
// using ModelContextProtocol.Server;
// using MssqlMcpServer.Infrastructure;
// namespace MssqlMcpServer.Tools;

// #nullable enable

// // สมมติว่า Attributes/Types ต่อไปนี้มาจากเฟรมเวิร์ก MCP ของคุณ:
// // [McpServerToolType], [McpServerTool], SqlRunner, RunProcedureRequest, DescribeRequest
// // และมีคลาส McpTools (generic) ของคุณอยู่แล้ว
// // คลาสนี้โฟกัสเฉพาะ "Loan Query Procedures" ที่ออกแบบไว้ 15 ตัว

// [McpServerToolType]
// public static class McpLoanQueryTools
// {
//     // ชื่อ Stored Procedure ให้เป็น constants กันพิมพ์ผิด
//     private static class Proc
//     {
//         public const string QryInterestMethods = "dbo.QryInterestMethods";
//         public const string QryCustomers = "dbo.QryCustomers";
//         public const string QryCustomerWithLoans = "dbo.QryCustomerWithLoans";
//         public const string QryLoanApplications = "dbo.QryLoanApplications";
//         public const string QryLoans = "dbo.QryLoans";
//         public const string QryLoanOverview = "dbo.QryLoanOverview";
//         public const string QryPaymentScheduleByLoan = "dbo.QryPaymentScheduleByLoan";
//         public const string QryPaymentsByLoan = "dbo.QryPaymentsByLoan";
//         public const string QryPaymentsByDate = "dbo.QryPaymentsByDate";
//         public const string QryUpcomingDue = "dbo.QryUpcomingDue";
//         public const string QryDelinquencyAging = "dbo.QryDelinquencyAging";
//         public const string QryProductPortfolioSummary = "dbo.QryProductPortfolioSummary";
//         public const string QryCollectorQueue = "dbo.QryCollectorQueue";
//         public const string QryPrepaymentHistory = "dbo.QryPrepaymentHistory";
//         public const string QryLoanBalanceAsOf = "dbo.QryLoanBalanceAsOf";
//     }


//     private static Dictionary<string, object> P(params (string Key, object? Val)[] kvs)
//     {
//         var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
//         foreach (var (k, v) in kvs)
//         {
//             if (v is not null) d[k] = v!;
//         }
//         return d;
//     }

//     // 1) Interest methods
//     [McpServerTool, Description(
//         "Purpose: ดึงรายการวิธีคิดดอกเบี้ยทั้งหมด หรือระบุเพื่อดูรายละเอียดเฉพาะรายการ.\n" +
//         "Params: interest_method_id (int?, optional) = รหัสวิธีคิดดอกเบี้ย; ถ้าไม่ระบุจะคืนทั้งหมด.")]
//     public static async Task<object> qry_interest_methods(
//         SqlRunner db,
//         [Description("รหัสวิธีคิดดอกเบี้ย (optional). ตัวอย่าง: 1; ไม่ระบุ = คืนทั้งหมด")] int? interest_method_id = null)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryInterestMethods,
//             Params = P(("interest_method_id", interest_method_id))
//         });

//     // 2) Customers (keyword + paging)
//     [McpServerTool, Description(
//         "Purpose: ค้นหาลูกค้าตามคีย์เวิร์ด (name/citizen_id/phone/email) พร้อมแบ่งหน้า.\n" +
//         "Params: keyword (string, optional, default \"\") = คำค้นบางส่วน (LIKE).\n" +
//         "        page (int, required, default 1) = เลขหน้าเริ่มที่ 1.\n" +
//         "        pageSize (int, required, default 20) = จำนวนต่อหน้า (แนะนำ 10–100).")]
//     public static async Task<object> qry_customers(
//         SqlRunner db,
//         [Description("คำค้นบางส่วน (LIKE) สำหรับ name/citizen_id/phone/email. ตัวอย่าง: \"som\"")] string? keyword = "",
//         [Description("เลขหน้าแบบ 1-based (>=1). ตัวอย่าง: 1")] int page = 1,
//         [Description("ขนาดหน้า (จำนวนเรคอร์ดต่อหน้า). ค่าเริ่มต้น 20")] int pageSize = 20)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryCustomers,
//             Params = P(("keyword", keyword ?? string.Empty), ("page", page), ("pagesize", pageSize))
//         });

//     // 3) Customer with loans
//     [McpServerTool, Description(
//         "Purpose: ดึงข้อมูลลูกค้าหนึ่งคนพร้อมรายการสินเชื่อทั้งหมดที่เกี่ยวข้อง (2 result sets: Customer, Loans).\n" +
//         "Params: customer_id (int, required) = รหัสลูกค้าเป้าหมาย.")]
//     public static async Task<object> qry_customer_with_loans(
//         SqlRunner db,
//         [Description("รหัสลูกค้า (required). ตัวอย่าง: 1")] int customer_id)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryCustomerWithLoans,
//             Params = P(("customer_id", customer_id))
//         });

//     // 4) Loan applications (filter by status/date range)
//     [McpServerTool, Description(
//         "Purpose: รายการคำขอสินเชื่อ โดยกรองตามสถานะและช่วงวันที่ยื่นคำขอ.\n" +
//         "Params: status (string?, optional) = สถานะ เช่น \"Approved\", \"Pending\"; null = ทั้งหมด.\n" +
//         "        date_from (DateTime?, optional) = เริ่มช่วง application_date (inclusive).\n" +
//         "        date_to (DateTime?, optional) = สิ้นสุดช่วง application_date (inclusive).")]
//     public static async Task<object> qry_loan_applications(
//         SqlRunner db,
//         [Description("ตัวกรองสถานะ เช่น Approved/Pending; ไม่ระบุ = ทั้งหมด")] string? status = null,
//         [Description("จาก application_date (yyyy-MM-dd), optional")] DateTime? date_from = null,
//         [Description("ถึง application_date (yyyy-MM-dd), optional")] DateTime? date_to = null)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryLoanApplications,
//             Params = P(("status", status), ("date_from", date_from), ("date_to", date_to))
//         });

//     // 5) Loans (filter by status/product/date range)
//     [McpServerTool, Description(
//         "Purpose: ค้นหาสัญญาเงินกู้ตามสถานะ/ผลิตภัณฑ์/ช่วงวันเริ่มสัญญา.\n" +
//         "Params: status (string?, optional) = สถานะสัญญา เช่น \"ACTIVE\", \"Closed\"; null = ทั้งหมด.\n" +
//         "        product_id (int?, optional) = รหัสผลิตภัณฑ์สินเชื่อ; null = ทุกผลิตภัณฑ์.\n" +
//         "        start_from (DateTime?, optional) = เริ่มช่วง start_date (inclusive).\n" +
//         "        start_to (DateTime?, optional) = สิ้นสุดช่วง start_date (inclusive).")]
//     public static async Task<object> qry_loans(
//         SqlRunner db,
//         [Description("สถานะสัญญา เช่น ACTIVE/Closed; ไม่ระบุ = ทั้งหมด")] string? status = null,
//         [Description("รหัสผลิตภัณฑ์สินเชื่อ; ไม่ระบุ = ทุกผลิตภัณฑ์")] int? product_id = null,
//         [Description("เริ่มช่วง start_date (yyyy-MM-dd), optional")] DateTime? start_from = null,
//         [Description("สิ้นสุดช่วง start_date (yyyy-MM-dd), optional")] DateTime? start_to = null)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryLoans,
//             Params = P(("status", status), ("product_id", product_id),
//                            ("start_from", start_from), ("start_to", start_to))
//         });

//     // 6) Loan overview
//     [McpServerTool, Description(
//         "Purpose: ภาพรวมสัญญาเงินกู้รายสัญญา — ยอดตั้งชำระ (scheduled) เทียบกับยอดที่จ่ายและยอดคงเหลือโดยประมาณ.\n" +
//         "Params: loan_id (int, required) = รหัสสัญญา.")]
//     public static async Task<object> qry_loan_overview(
//         SqlRunner db,
//         [Description("รหัสสัญญาเงินกู้ (required). ตัวอย่าง: 1")] int loan_id)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryLoanOverview,
//             Params = P(("loan_id", loan_id))
//         });

//     // 7) Payment schedule by loan
//     [McpServerTool, Description(
//         "Purpose: แสดงตารางงวดชำระของสัญญา พร้อมจำนวนวันค้างชำระ (days late) และ aging bucket ต่อรายการ.\n" +
//         "Params: loan_id (int, required) = รหัสสัญญา.")]
//     public static async Task<object> qry_payment_schedule_by_loan(
//         SqlRunner db,
//         [Description("รหัสสัญญาเงินกู้ (required). ตัวอย่าง: 1")] int loan_id)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryPaymentScheduleByLoan,
//             Params = P(("loan_id", loan_id))
//         });

//     // 8) Payments by loan
//     [McpServerTool, Description(
//         "Purpose: แสดงประวัติการชำระเงินทั้งหมดของสัญญาพร้อมข้อมูลงวดที่เกี่ยวข้อง.\n" +
//         "Params: loan_id (int, required) = รหัสสัญญา.")]
//     public static async Task<object> qry_payments_by_loan(
//         SqlRunner db,
//         [Description("รหัสสัญญาเงินกู้ (required). ตัวอย่าง: 1")] int loan_id)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryPaymentsByLoan,
//             Params = P(("loan_id", loan_id))
//         });

//     // 9) Payments by date range/method
//     [McpServerTool, Description(
//         "Purpose: รายงานการชำระเงินในช่วงวันที่ที่กำหนด และสามารถกรองตามวิธีชำระเงินได้.\n" +
//         "Params: date_from (string, required) = เริ่มช่วง payment_date ('yyyy-MM-dd').\n" +
//         "        date_to (string, required) = สิ้นสุดช่วง payment_date ('yyyy-MM-dd').\n" +
//         "        payment_method_id (int?, optional) = วิธีชำระเงิน; null = ทุกวิธี.(1:CASH,2:TRANSFER,3:CARD)")]
//     public static async Task<object> qry_payments_by_date(
//         SqlRunner db,
//         [Description("จาก payment_date (yyyy-MM-dd), inclusive (required)")] DateTime date_from,
//         [Description("ถึง payment_date (yyyy-MM-dd), inclusive (required)")] DateTime date_to,
//         [Description("รหัสวิธีชำระเงิน (optional). ตัวอย่าง: 2=TRANSFER; ไม่ระบุ=ทุกวิธี")] int? payment_method_id = null)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryPaymentsByDate,
//             Params = P(("date_from", date_from), ("date_to", date_to),
//                            ("payment_method_id", payment_method_id))
//         });

//     // 10) Upcoming due in X days
//     [McpServerTool, Description(
//         "Purpose: แสดงงวดที่กำลังจะถึงกำหนดชำระภายใน N วันข้างหน้า พร้อมยอดค้าง PI ต่อรายการ.\n" +
//         "Params: days_ahead (int, optional, default 7) = จำนวนวันนับจากวันนี้ (>=0).")]
//     public static async Task<object> qry_upcoming_due(
//         SqlRunner db,
//         [Description("จำนวนวันข้างหน้า (default 7). ตัวอย่าง: 7/30/60")] int days_ahead = 7)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryUpcomingDue,
//             Params = P(("days_ahead", days_ahead))
//         });

//     // 11) Delinquency aging
//     [McpServerTool, Description(
//         "Purpose: สรุปสถานะค้างชำระทั้งพอร์ต แบ่งตาม aging bucket (Current/1-30/31-60/61-90/90+).\n" +
//         "Params: (none) — คืนจำนวนงวดและยอดคงค้างรวมต่อ bucket.")]
//     public static async Task<object> qry_delinquency_aging(SqlRunner db)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryDelinquencyAging,
//             Params = null
//         });

//     // 12) Product portfolio summary
//     [McpServerTool, Description(
//         "Purpose: สรุปพอร์ตตามผลิตภัณฑ์ (จำนวนสัญญา, ยอดปล่อยรวม, ยอด PI คงค้างประมาณการ).\n" +
//         "Params: (none). ใช้งานเพื่อดูภาพรวมแยกตาม product.")]
//     public static async Task<object> qry_product_portfolio_summary(SqlRunner db)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryProductPortfolioSummary,
//             Params = null
//         });

//     // 13) Collector queue (min days late)
//     [McpServerTool, Description(
//         "Purpose: จัดคิวติดตามหนี้ โดยเลือกเฉพาะงวดที่ค้างชำระอย่างน้อย X วันขึ้นไป.\n" +
//         "Params: min_days_late (int, optional, default 1) = เกณฑ์วันค้างชำระขั้นต่ำ (>=0).")]
//     public static async Task<object> qry_collector_queue(
//         SqlRunner db,
//         [Description("วันค้างชำระขั้นต่ำ (default 1). ตัวอย่าง: 1/7/30/60")] int min_days_late = 1)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryCollectorQueue,
//             Params = P(("min_days_late", min_days_late))
//         });

//     // 14) Prepayment history (by loan optional)
//     [McpServerTool, Description(
//         "Purpose: แสดงรายการงวดที่มีการจ่ายเกินยอดกำหนด (amount_paid > total_due).\n" +
//         "Params: loan_id (int?, optional) = ระบุสัญญาเพื่อกรอง; null = ทุกสัญญา.")]
//     public static async Task<object> qry_prepayment_history(
//         SqlRunner db,
//         [Description("รหัสสัญญา (optional). ไม่ระบุ = ทั้งหมด")] int? loan_id = null)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryPrepaymentHistory,
//             Params = P(("loan_id", loan_id))
//         });

//     // 15) Loan balance snapshot as-of date
//     [McpServerTool, Description(
//         "Purpose: ภาพรวมยอดคงเหลือเงินต้นของสัญญา ณ วันที่กำหนด (as-of) พร้อมสเตตัสงวดถึงวันดังกล่าว.\n" +
//         "Params: loan_id (int, required) = รหัสสัญญา.\n" +
//         "        as_of (DateTime, required, yyyy-MM-dd) = วันที่อ้างอิง (inclusive).")]
//     public static async Task<object> qry_loan_balance_asof(
//         SqlRunner db,
//         [Description("รหัสสัญญา (required). ตัวอย่าง: 1")] int loan_id,
//         [Description("วันที่อ้างอิงรูปแบบ yyyy-MM-dd (required). ตัวอย่าง: 2025-06-30")] DateTime as_of)
//         => await db.RunProcedureAsync(new RunProcedureRequest
//         {
//             ProcedureName = Proc.QryLoanBalanceAsOf,
//             Params = P(("loan_id", loan_id), ("as_of", as_of))
//         });

// }
