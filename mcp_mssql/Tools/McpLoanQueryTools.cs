using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using MssqlMcpServer.Infrastructure;
namespace MssqlMcpServer.Tools;

#nullable enable

// สมมติว่า Attributes/Types ต่อไปนี้มาจากเฟรมเวิร์ก MCP ของคุณ:
// [McpServerToolType], [McpServerTool], SqlRunner, RunProcedureRequest, DescribeRequest
// และมีคลาส McpTools (generic) ของคุณอยู่แล้ว
// คลาสนี้โฟกัสเฉพาะ "Loan Query Procedures" ที่ออกแบบไว้ 15 ตัว

[McpServerToolType]
public static class McpLoanQueryTools
{
    // ชื่อ Stored Procedure ให้เป็น constants กันพิมพ์ผิด
    private static class Proc
    {
        public const string QryInterestMethods = "dbo.QryInterestMethods";
        public const string QryCustomers = "dbo.QryCustomers";
        public const string QryCustomerWithLoans = "dbo.QryCustomerWithLoans";
        public const string QryLoanApplications = "dbo.QryLoanApplications";
        public const string QryLoans = "dbo.QryLoans";
        public const string QryLoanOverview = "dbo.QryLoanOverview";
        public const string QryPaymentScheduleByLoan = "dbo.QryPaymentScheduleByLoan";
        public const string QryPaymentsByLoan = "dbo.QryPaymentsByLoan";
        public const string QryPaymentsByDate = "dbo.QryPaymentsByDate";
        public const string QryUpcomingDue = "dbo.QryUpcomingDue";
        public const string QryDelinquencyAging = "dbo.QryDelinquencyAging";
        public const string QryProductPortfolioSummary = "dbo.QryProductPortfolioSummary";
        public const string QryCollectorQueue = "dbo.QryCollectorQueue";
        public const string QryPrepaymentHistory = "dbo.QryPrepaymentHistory";
        public const string QryLoanBalanceAsOf = "dbo.QryLoanBalanceAsOf";
    }


    private static Dictionary<string, object> P(params (string Key, object? Val)[] kvs)
    {
        var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in kvs)
        {
            if (v is not null) d[k] = v!;
        }
        return d;
    }

    // 1) Interest methods
    [McpServerTool, Description(
    "Purpose: ดึงรายการวิธีคิดดอกเบี้ยทั้งหมด หรือระบุเพื่อดูรายละเอียดเฉพาะรายการ.\n" +
    "Params: interest_method_id (int?, optional) = รหัสวิธีคิดดอกเบี้ย; ถ้าไม่ระบุจะคืนทั้งหมด.\n" +
    "Result: คืนเป็น list ของ row ในรูปแบบ:\n" +
    "  [interest_method_id, name, description, formula_reference]\n" +
    "    - interest_method_id (int): รหัสวิธีคิดดอกเบี้ย\n" +
    "    - name (string): ชื่อวิธีคิดดอกเบี้ย\n" +
    "    - description (string): รายละเอียดวิธีคิดดอกเบี้ย\n" +
    "    - formula_reference (string): อ้างอิงสูตรหรือ logic ที่ใช้คำนวณ")]
    public static async Task<object> qry_interest_methods(
        SqlRunner db,
        [Description("รหัสวิธีคิดดอกเบี้ย (optional). ตัวอย่าง: 1; ไม่ระบุ = คืนทั้งหมด")] int? interest_method_id = null)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryInterestMethods,
            Params = P(("interest_method_id", interest_method_id))
        });

    // 2) Customers (keyword + paging)
    [McpServerTool, Description(
    "Purpose: ค้นหาลูกค้าตามคีย์เวิร์ด (name/citizen_id/phone/email) พร้อมแบ่งหน้า.\n" +
    "Params:\n" +
    "  keyword (string, optional, default \"\") = คำค้นบางส่วน (LIKE).\n" +
    "  page (int, required, default 1) = เลขหน้าเริ่มที่ 1.\n" +
    "  pageSize (int, required, default 20) = จำนวนต่อหน้า (แนะนำ 10–100).\n" +
    "Result: คืนข้อมูลลูกค้าแบบแบ่งหน้าเป็น list ของ row ดังนี้:\n" +
    "  [customer_id, name, citizen_id, phone, email, address, total_count, page, page_size]\n" +
    "    - customer_id (int): รหัสลูกค้า\n" +
    "    - name (string): ชื่อลูกค้า\n" +
    "    - citizen_id (string): เลขบัตรประชาชน\n" +
    "    - phone (string): เบอร์โทร\n" +
    "    - email (string): อีเมลติดต่อ\n" +
    "    - address (string): ที่อยู่ลูกค้า\n" +
    "    - total_count (int): จำนวนเรคอร์ดทั้งหมดก่อนแบ่งหน้า\n" +
    "    - page (int): หน้าปัจจุบัน\n" +
    "    - page_size (int): จำนวนเรคอร์ดต่อหน้า")]
    public static async Task<object> qry_customers(
        SqlRunner db,
        [Description("คำค้นบางส่วน (LIKE) สำหรับ name/citizen_id/phone/email. ตัวอย่าง: \"som\"")] string? keyword = "",
        [Description("เลขหน้าแบบ 1-based (>=1). ตัวอย่าง: 1")] int page = 1,
        [Description("ขนาดหน้า (จำนวนเรคอร์ดต่อหน้า). ค่าเริ่มต้น 20")] int pageSize = 20)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryCustomers,
            Params = P(("keyword", keyword ?? string.Empty), ("page", page), ("pagesize", pageSize))
        });

    // 3) Customer with loans
    [McpServerTool, Description(
    "Purpose: ดึงข้อมูลลูกค้าหนึ่งคนพร้อมรายการสินเชื่อทั้งหมดที่เกี่ยวข้อง (2 result sets: Customer, Loans).\n" +
    "Params:\n" +
    "  customer_id (int, required) = รหัสลูกค้าเป้าหมาย.\n" +
    "Result:\n" +
    "  ResultSet[0] = ข้อมูลลูกค้า 1 row ในรูปแบบ:\n" +
    "    [customer_id, name, citizen_id, phone, email, address]\n" +
    "      - customer_id (int): รหัสลูกค้า\n" +
    "      - name (string): ชื่อลูกค้า\n" +
    "      - citizen_id (string): เลขบัตรประชาชน\n" +
    "      - phone (string): เบอร์โทร\n" +
    "      - email (string): อีเมลติดต่อ\n" +
    "      - address (string): ที่อยู่ลูกค้า\n" +
    "\n" +
    "  ResultSet[1] = รายการสินเชื่อของลูกค้ารายนั้น (0..N rows) ในรูปแบบ:\n" +
    "    [loan_id, loan_no, status, principal_amount, term_months, start_date, end_date,\n" +
    "     product_code, product_id, product_name_th, product_name_en]\n" +
    "      - loan_id (int): รหัสสินเชื่อ\n" +
    "      - loan_no (string): เลขที่สัญญา/เลขที่สินเชื่อ (เช่น LO20250215-0005)\n" +
    "      - status (string): สถานะสินเชื่อ (เช่น ACTIVE, CLOSED)\n" +
    "      - principal_amount (decimal): วงเงิน/เงินต้นของสินเชื่อ\n" +
    "      - term_months (int): ระยะเวลาผ่อน (หน่วยเป็นเดือน)\n" +
    "      - start_date (date): วันที่เริ่มสัญญา\n" +
    "      - end_date (date): วันที่สิ้นสุดสัญญา\n" +
    "      - product_code (string): รหัสหรือโค้ดประเภทสินเชื่อ (เช่น DIM)\n" +
    "      - product_id (int): รหัสประเภท/ผลิตภัณฑ์สินเชื่อ\n" +
    "      - product_name_th (string): ชื่อสินเชื่อภาษาไทย (เช่น สินเชื่อมอเตอร์ไซค์)\n" +
    "      - product_name_en (string): ชื่อสินเชื่อภาษาอังกฤษ (เช่น Motorcycle)")]
    public static async Task<object> qry_customer_with_loans(
        SqlRunner db,
        [Description("รหัสลูกค้า (required). ตัวอย่าง: 1")] int customer_id)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryCustomerWithLoans,
            Params = P(("customer_id", customer_id))
        });

    // 4) Loan applications (filter by status/date range)
    [McpServerTool, Description(
    "Purpose: รายการคำขอสินเชื่อ โดยกรองตามสถานะและช่วงวันที่ยื่นคำขอ.\n" +
    "Params:\n" +
    "  status (string?, optional) = สถานะ เช่น \"Approved\", \"Pending\"; null = ทั้งหมด.\n" +
    "  date_from (DateTime?, optional) = เริ่มช่วง application_date (inclusive).\n" +
    "  date_to (DateTime?, optional) = สิ้นสุดช่วง application_date (inclusive).\n" +
    "Result: คืนเป็น list ของ row ในรูปแบบ:\n" +
    "  [application_id, application_date, status, requested_amount, approved_amount,\n" +
    "   approved_date, customer_id, customer_name, loan_product_id, product_name, sub_type]\n" +
    "    - application_id (int): รหัสคำขอสินเชื่อ\n" +
    "    - application_date (date): วันที่ยื่นคำขอสินเชื่อ\n" +
    "    - status (string): สถานะคำขอ (เช่น Approved, Pending, Rejected)\n" +
    "    - requested_amount (decimal): วงเงินที่ลูกค้าขอ\n" +
    "    - approved_amount (decimal): วงเงินที่อนุมัติจริง\n" +
    "    - approved_date (date?): วันที่อนุมัติ (อาจเป็น null ถ้ายังไม่อนุมัติ)\n" +
    "    - customer_id (int): รหัสลูกค้า\n" +
    "    - customer_name (string): ชื่อลูกค้า\n" +
    "    - loan_product_id (int): รหัสผลิตภัณฑ์สินเชื่อที่ขอ\n" +
    "    - product_name (string): ชื่อผลิตภัณฑ์สินเชื่อ (เช่น สินเชื่อมอเตอร์ไซค์)\n" +
    "    - sub_type (string): ประเภท/กลุ่มย่อยของสินเชื่อ (เช่น Motorcycle)")]
    public static async Task<object> qry_loan_applications(
        SqlRunner db,
        [Description("ตัวกรองสถานะ เช่น Approved/Pending; ไม่ระบุ = ทั้งหมด")] string? status = null,
        [Description("จาก application_date (yyyy-MM-dd), optional")] DateTime? date_from = null,
        [Description("ถึง application_date (yyyy-MM-dd), optional")] DateTime? date_to = null)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryLoanApplications,
            Params = P(("status", status), ("date_from", date_from), ("date_to", date_to))
        });

    // 5) Loans (filter by status/product/date range)
    [McpServerTool, Description(
    "Purpose: ค้นหาสัญญาเงินกู้ตามสถานะ/ผลิตภัณฑ์/ช่วงวันเริ่มสัญญา.\n" +
    "Params:\n" +
    "  status (string?, optional) = สถานะสัญญา เช่น \"ACTIVE\", \"Closed\"; null = ทั้งหมด.\n" +
    "  product_id (int?, optional) = รหัสผลิตภัณฑ์สินเชื่อ; null = ทุกผลิตภัณฑ์.\n" +
    "  start_from (DateTime?, optional) = เริ่มช่วง start_date (inclusive).\n" +
    "  start_to (DateTime?, optional) = สิ้นสุดช่วง start_date (inclusive).\n" +
    "\n" +
    "Result: คืนเป็น list ของสัญญาเงินกู้แบบ row structure:\n" +
    "  [loan_id, contract_number, status, loan_amount, loan_term, start_date, end_date,\n" +
    "   customer_id, customer_name, loan_product_id, product_name, sub_type, interest_method]\n" +
    "\n" +
    "    - loan_id (int): รหัสสินเชื่อ\n" +
    "    - contract_number (string): เลขที่สัญญา เช่น LO20250208-0004\n" +
    "    - status (string): สถานะสัญญา เช่น ACTIVE, Closed\n" +
    "    - loan_amount (decimal): วงเงินกู้ / เงินต้น\n" +
    "    - loan_term (int): ระยะเวลาผ่อน (เดือน)\n" +
    "    - start_date (date): วันที่เริ่มสัญญา\n" +
    "    - end_date (date): วันที่สิ้นสุดสัญญา\n" +
    "    - customer_id (int): รหัสลูกค้า\n" +
    "    - customer_name (string): ชื่อลูกค้า\n" +
    "    - loan_product_id (int): รหัสผลิตภัณฑ์สินเชื่อ\n" +
    "    - product_name (string): ชื่อผลิตภัณฑ์สินเชื่อ เช่น สินเชื่อรถยนต์\n" +
    "    - sub_type (string): กลุ่มย่อย เช่น Car\n" +
    "    - interest_method (string): วิธีคิดดอกเบี้ย เช่น FLAT 15%")]
    public static async Task<object> qry_loans(
        SqlRunner db,
        [Description("สถานะสัญญา เช่น ACTIVE/Closed; ไม่ระบุ = ทั้งหมด")] string? status = null,
        [Description("รหัสผลิตภัณฑ์สินเชื่อ; ไม่ระบุ = ทุกผลิตภัณฑ์")] int? product_id = null,
        [Description("เริ่มช่วง start_date (yyyy-MM-dd), optional")] DateTime? start_from = null,
        [Description("สิ้นสุดช่วง start_date (yyyy-MM-dd), optional")] DateTime? start_to = null)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryLoans,
            Params = P(("status", status), ("product_id", product_id),
                           ("start_from", start_from), ("start_to", start_to))
        });

    // 6) Loan overview
    [McpServerTool, Description(
    "Purpose: ภาพรวมสัญญาเงินกู้รายสัญญา — ยอดตั้งชำระ (scheduled) เทียบกับยอดที่จ่ายและยอดคงเหลือโดยประมาณ.\n" +
    "Params: loan_id (int, required) = รหัสสัญญา.\n" +
    "\n" +
    "Result: คืน 1 row แสดงภาพรวมสัญญาในรูปแบบ:\n" +
    "  [loan_id, interest_method_id, contract_number, loan_amount, loan_term,\n" +
    "   start_date, end_date, status,\n" +
    "   total_principal_scheduled, total_interest_scheduled, total_scheduled,\n" +
    "   total_principal_paid, total_interest_paid, total_penalty_paid,\n" +
    "   total_amount_paid, total_sched_outstanding, principal_balance_estimate]\n" +
    "\n" +
    "    - loan_id (int): รหัสสัญญาเงินกู้\n" +
    "    - interest_method_id (int): รหัสวิธีคิดดอกเบี้ยของสัญญา\n" +
    "    - contract_number (string): เลขที่สัญญา เช่น LO20250105-0002\n" +
    "    - loan_amount (decimal): วงเงินกู้/เงินต้นตั้งต้น\n" +
    "    - loan_term (int): จำนวนงวด (เดือน)\n" +
    "    - start_date (date): วันเริ่มสัญญา\n" +
    "    - end_date (date): วันสิ้นสุดสัญญา\n" +
    "    - status (string): สถานะ เช่น ACTIVE, Closed\n" +
    "\n" +
    "    # Scheduled Totals\n" +
    "    - total_principal_scheduled (decimal): ยอดตั้งชำระเงินต้นรวม\n" +
    "    - total_interest_scheduled (decimal): ยอดตั้งชำระดอกเบี้ยรวม\n" +
    "    - total_scheduled (decimal): ยอดตั้งชำระรวมทั้งหมด (principal + interest)\n" +
    "\n" +
    "    # Paid Totals\n" +
    "    - total_principal_paid (decimal): เงินต้นที่จ่ายแล้วรวม\n" +
    "    - total_interest_paid (decimal): ดอกเบี้ยที่จ่ายแล้วรวม\n" +
    "    - total_penalty_paid (decimal): ค่าปรับที่จ่ายแล้วรวม\n" +
    "    - total_amount_paid (decimal): ยอดรวมที่จ่ายทั้งหมด\n" +
    "\n" +
    "    # Outstanding / Estimate\n" +
    "    - total_sched_outstanding (decimal): ยอดตั้งชำระที่ยังไม่ชำระ\n" +
    "    - principal_balance_estimate (decimal): เงินต้นคงเหลือโดยประมาณ (estimate)")]
    public static async Task<object> qry_loan_overview(
        SqlRunner db,
        [Description("รหัสสัญญาเงินกู้ (required). ตัวอย่าง: 1")] int loan_id)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryLoanOverview,
            Params = P(("loan_id", loan_id))
        });

    // 7) Payment schedule by loan
    [McpServerTool, Description(
    "Purpose: แสดงตารางงวดชำระของสัญญา พร้อมจำนวนวันค้างชำระ (days late) และ aging bucket ต่อรายการ.\n" +
    "Params:\n" +
    "  loan_id (int, required) = รหัสสัญญา.\n" +
    "\n" +
    "Result: คืนเป็น list ของงวดชำระ โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [schedule_id, loan_id, due_date, principal_due, interest_due, total_due, status,\n" +
    "   principal_paid, interest_paid, penalty_paid, amount_paid,\n" +
    "   outstanding_pi, days_late, aging_bucket, last_payment_date]\n" +
    "\n" +
    "    - schedule_id (int): รหัสงวดชำระ\n" +
    "    - loan_id (int): รหัสสัญญาเงินกู้\n" +
    "    - due_date (date): กำหนดชำระของงวด\n" +
    "    - principal_due (decimal): เงินต้นที่ต้องชำระงวดนี้\n" +
    "    - interest_due (decimal): ดอกเบี้ยที่ต้องชำระงวดนี้\n" +
    "    - total_due (decimal): ยอดรวมที่ต้องชำระ (principal + interest)\n" +
    "    - status (string): สถานะของงวด เช่น Paid, Pending, Overdue\n" +
    "\n" +
    "    # Paid Details\n" +
    "    - principal_paid (decimal): เงินต้นที่จ่ายแล้วของงวดนี้\n" +
    "    - interest_paid (decimal): ดอกเบี้ยที่จ่ายแล้วของงวดนี้\n" +
    "    - penalty_paid (decimal): ค่าปรับที่จ่ายแล้วของงวดนี้\n" +
    "    - amount_paid (decimal): รวมยอดที่จ่ายแล้วทั้งหมด\n" +
    "\n" +
    "    # Outstanding & Aging\n" +
    "    - outstanding_pi (decimal): เงินต้น + ดอกเบี้ยที่ยังค้างอยู่ของงวดนี้\n" +
    "    - days_late (int): จำนวนวันค้างชำระ (0 = ไม่ค้าง)\n" +
    "    - aging_bucket (string): กลุ่มอายุหนี้ เช่น Current, 1–30, 31–60, 60+\n" +
    "\n" +
    "    - last_payment_date (date?): วันที่มีการจ่ายล่าสุดของงวด (null = ยังไม่เคยจ่าย)")]
    public static async Task<object> qry_payment_schedule_by_loan(
        SqlRunner db,
        [Description("รหัสสัญญาเงินกู้ (required). ตัวอย่าง: 1")] int loan_id)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryPaymentScheduleByLoan,
            Params = P(("loan_id", loan_id))
        });

    // 8) Payments by loan
    [McpServerTool, Description(
    "Purpose: แสดงประวัติการชำระเงินทั้งหมดของสัญญาพร้อมข้อมูลงวดที่เกี่ยวข้อง.\n" +
    "Params:\n" +
    "  loan_id (int, required) = รหัสสัญญา.\n" +
    "\n" +
    "Result: คืนเป็น list ของรายการชำระเงิน โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [payment_id, schedule_id, payment_date, amount_paid,\n" +
    "   principal_paid, interest_paid, penalty_paid,\n" +
    "   payment_method_id, payment_method,\n" +
    "   due_date, total_due, status]\n" +
    "\n" +
    "    - payment_id (int): รหัสรายการชำระเงิน\n" +
    "    - schedule_id (int): รหัสงวดที่รายการนี้ผูกกับ\n" +
    "    - payment_date (date): วันที่ชำระ\n" +
    "    - amount_paid (decimal): ยอดรวมที่ชำระ\n" +
    "\n" +
    "    # Breakdown\n" +
    "    - principal_paid (decimal): เงินต้นที่ชำระ\n" +
    "    - interest_paid (decimal): ดอกเบี้ยที่ชำระ\n" +
    "    - penalty_paid (decimal): ค่าปรับที่ชำระ\n" +
    "\n" +
    "    # Payment Method\n" +
    "    - payment_method_id (int): รหัสช่องทางชำระ\n" +
    "    - payment_method (string): ชื่อช่องทาง เช่น TRANSFER\n" +
    "\n" +
    "    # Schedule Context\n" +
    "    - due_date (date): กำหนดชำระของงวดที่เกี่ยวข้อง\n" +
    "    - total_due (decimal): ยอดต้องชำระของงวด\n" +
    "    - status (string): สถานะการชำระของงวด (เช่น Paid, Pending)")]
    public static async Task<object> qry_payments_by_loan(
        SqlRunner db,
        [Description("รหัสสัญญาเงินกู้ (required). ตัวอย่าง: 1")] int loan_id)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryPaymentsByLoan,
            Params = P(("loan_id", loan_id))
        });

    // 9) Payments by date range/method
    [McpServerTool, Description(
    "Purpose: รายงานการชำระเงินในช่วงวันที่ที่กำหนด และสามารถกรองตามวิธีชำระเงินได้.\n" +
    "Params:\n" +
    "  date_from (string, required) = เริ่มช่วง payment_date ('yyyy-MM-dd').\n" +
    "  date_to (string, required) = สิ้นสุดช่วง payment_date ('yyyy-MM-dd').\n" +
    "  payment_method_id (int?, optional) = วิธีชำระเงิน; null = ทุกวิธี (1:CASH, 2:TRANSFER, 3:CARD).\n" +
    "\n" +
    "Result: คืนเป็น list ของประวัติชำระเงินในช่วงวันที่ โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [payment_id, schedule_id, payment_date, amount_paid,\n" +
    "   principal_paid, interest_paid, penalty_paid,\n" +
    "   payment_method_id, payment_method,\n" +
    "   loan_id, due_date, total_due, contract_number,\n" +
    "   customer_id, customer_name]\n" +
    "\n" +
    "    - payment_id (int): รหัสรายการชำระเงิน\n" +
    "    - schedule_id (int): รหัสงวดที่รายการนี้ผูก (nullable ได้)\n" +
    "    - payment_date (date): วันที่ชำระเงิน\n" +
    "    - amount_paid (decimal): ยอดรวมที่ชำระ\n" +
    "    - principal_paid (decimal): เงินต้นที่ชำระในรายการ\n" +
    "    - interest_paid (decimal): ดอกเบี้ยที่ชำระ\n" +
    "    - penalty_paid (decimal): ค่าปรับที่ชำระ\n" +
    "\n" +
    "    - payment_method_id (int): ช่องทางการชำระ (1:CASH, 2:TRANSFER, 3:CARD)\n" +
    "    - payment_method (string): ชื่อช่องทาง เช่น Cash, Transfer, Card\n" +
    "\n" +
    "    - loan_id (int): รหัสสัญญาเงินกู้\n" +
    "    - due_date (date): กำหนดชำระของงวดที่เกี่ยวข้อง\n" +
    "    - total_due (decimal): ยอดต้องชำระของงวดนั้น (principal + interest)\n" +
    "    - contract_number (string): เลขที่สัญญาเงินกู้\n" +
    "\n" +
    "    - customer_id (int): รหัสลูกค้า\n" +
    "    - customer_name (string): ชื่อลูกค้า")]
    public static async Task<object> qry_payments_by_date(
        SqlRunner db,
        [Description("จาก payment_date (yyyy-MM-dd), inclusive (required)")] DateTime date_from,
        [Description("ถึง payment_date (yyyy-MM-dd), inclusive (required)")] DateTime date_to,
        [Description("รหัสวิธีชำระเงิน (optional). ตัวอย่าง: 2=TRANSFER; ไม่ระบุ=ทุกวิธี")] int? payment_method_id = null)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryPaymentsByDate,
            Params = P(("date_from", date_from), ("date_to", date_to),
                           ("payment_method_id", payment_method_id))
        });

    // 10) Upcoming due in X days
    [McpServerTool, Description(
    "Purpose: แสดงงวดที่กำลังจะถึงกำหนดชำระภายใน N วันข้างหน้า พร้อมยอดค้าง PI ต่อรายการ.\n" +
    "Params:\n" +
    "  days_ahead (int, optional, default 7) = จำนวนวันนับจากวันนี้ (>=0).\n" +
    "\n" +
    "Result: คืนเป็น list ของงวดที่ใกล้ถึงกำหนด โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [schedule_id, loan_id, due_date, total_due,\n" +
    "   outstanding_pi, contract_number,\n" +
    "   customer_id, customer_name]\n" +
    "\n" +
    "    - schedule_id (int): รหัสงวดชำระ\n" +
    "    - loan_id (int): รหัสสัญญาเงินกู้\n" +
    "    - due_date (date): วันที่งวดจะถึงกำหนดชำระ\n" +
    "    - total_due (decimal): ยอดที่ต้องชำระตามงวด (PI ทั้งหมด)\n" +
    "\n" +
    "    - outstanding_pi (decimal): ยอดค้างชำระ (principal + interest) ณ ปัจจุบัน\n" +
    "\n" +
    "    # Contract Context\n" +
    "    - contract_number (string): เลขที่สัญญา เช่น LO20250110-0001\n" +
    "\n" +
    "    # Customer Info\n" +
    "    - customer_id (int): รหัสลูกค้า\n" +
    "    - customer_name (string): ชื่อลูกค้า")]
    public static async Task<object> qry_upcoming_due(
        SqlRunner db,
        [Description("จำนวนวันข้างหน้า (default 7). ตัวอย่าง: 7/30/60")] int days_ahead = 7)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryUpcomingDue,
            Params = P(("days_ahead", days_ahead))
        });

    // 11) Delinquency aging
    [McpServerTool, Description(
    "Purpose: สรุปสถานะค้างชำระทั้งพอร์ต แบ่งตาม aging bucket (Current/1-30/31-60/61-90/90+).\n" +
    "Params: (none) — คืนจำนวนงวดและยอดคงค้างรวมต่อ bucket.\n" +
    "\n" +
    "Result: คืนเป็น list ของ bucket summary โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [aging_bucket, schedule_count, total_outstanding_pi]\n" +
    "\n" +
    "    - aging_bucket (string): กลุ่มอายุหนี้ เช่น\n" +
    "        * Current\n" +
    "        * 1-30\n" +
    "        * 31-60\n" +
    "        * 61-90\n" +
    "        * 90+\n" +
    "\n" +
    "    - schedule_count (int): จำนวนงวดใน bucket นั้น\n" +
    "    - total_outstanding_pi (decimal): ยอดค้างชำระรวม (principal + interest) ใน bucket นั้น")]
    public static async Task<object> qry_delinquency_aging(SqlRunner db)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryDelinquencyAging,
            Params = null
        });

    // 12) Product portfolio summary
    [McpServerTool, Description(
    "Purpose: สรุปพอร์ตตามผลิตภัณฑ์ (จำนวนสัญญา, ยอดปล่อยรวม, ยอด PI คงค้างประมาณการ).\n" +
    "Params: (none). ใช้งานเพื่อดูภาพรวมแยกตาม product.\n" +
    "\n" +
    "Result: คืนเป็น list ของข้อมูลสรุปพอร์ตตามผลิตภัณฑ์ โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [loan_product_id, product_name, sub_type,\n" +
    "   loans_count, total_disbursed, total_pi_outstanding]\n" +
    "\n" +
    "    - loan_product_id (int): รหัสผลิตภัณฑ์สินเชื่อ\n" +
    "    - product_name (string): ชื่อผลิตภัณฑ์สินเชื่อ (เช่น สินเชื่อมอเตอร์ไซค์)\n" +
    "    - sub_type (string): กลุ่มย่อยของผลิตภัณฑ์ (เช่น Motorcycle)\n" +
    "\n" +
    "    - loans_count (int): จำนวนสัญญาที่อยู่ในผลิตภัณฑ์นี้\n" +
    "    - total_disbursed (decimal): ยอดปล่อยสินเชื่อรวม\n" +
    "    - total_pi_outstanding (decimal): ยอดคงค้าง PI โดยประมาณ (principal + interest)")]
    public static async Task<object> qry_product_portfolio_summary(SqlRunner db)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryProductPortfolioSummary,
            Params = null
        });

    // 13) Collector queue (min days late)
    [McpServerTool, Description(
    "Purpose: จัดคิวติดตามหนี้ โดยเลือกเฉพาะงวดที่ค้างชำระอย่างน้อย X วันขึ้นไป.\n" +
    "Params:\n" +
    "  min_days_late (int, optional, default 1) = เกณฑ์วันค้างชำระขั้นต่ำ (>=0).\n" +
    "\n" +
    "Result: คืนเป็น list ของงวดที่เข้าเกณฑ์ติดตามหนี้ โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [schedule_id, loan_id, due_date, total_due,\n" +
    "   outstanding_pi, days_late,\n" +
    "   contract_number, customer_id, customer_name, phone]\n" +
    "\n" +
    "    - schedule_id (int): รหัสงวดชำระ\n" +
    "    - loan_id (int): รหัสสัญญาเงินกู้\n" +
    "    - due_date (date): วันกำหนดชำระของงวด\n" +
    "    - total_due (decimal): ยอดต้องชำระตามงวด (PI รวม)\n" +
    "\n" +
    "    - outstanding_pi (decimal): ยอดค้างชำระ (principal + interest)\n" +
    "    - days_late (int): จำนวนวันค้าง (>= min_days_late)\n" +
    "\n" +
    "    # Contract Context\n" +
    "    - contract_number (string): เลขที่สัญญา เช่น LO20250105-0002\n" +
    "\n" +
    "    # Customer Info\n" +
    "    - customer_id (int): รหัสลูกค้า\n" +
    "    - customer_name (string): ชื่อลูกค้า\n" +
    "    - phone (string): เบอร์โทรลูกค้า")]
    public static async Task<object> qry_collector_queue(
        SqlRunner db,
        [Description("วันค้างชำระขั้นต่ำ (default 1). ตัวอย่าง: 1/7/30/60")] int min_days_late = 1)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryCollectorQueue,
            Params = P(("min_days_late", min_days_late))
        });

    // 14) Prepayment history (by loan optional)
    [McpServerTool, Description(
    "Purpose: แสดงรายการงวดที่มีการจ่ายเกินยอดกำหนด (amount_paid > total_due).\n" +
    "Params:\n" +
    "  loan_id (int?, optional) = ระบุสัญญาเพื่อกรอง; null = ทุกสัญญา.\n" +
    "\n" +
    "Result: คืนเป็น list ของงวดที่มีการจ่ายเกิน โดยแต่ละ row มีโครงสร้าง:\n" +
    "  [schedule_id, loan_id, due_date, total_due,\n" +
    "   amount_paid, principal_paid, interest_paid, penalty_paid,\n" +
    "   last_payment_date, overpay_amount]\n" +
    "\n" +
    "    - schedule_id (int): รหัสงวด\n" +
    "    - loan_id (int): รหัสสัญญาเงินกู้\n" +
    "    - due_date (date): กำหนดชำระของงวด\n" +
    "    - total_due (decimal): ยอดที่ต้องชำระตามกำหนด\n" +
    "\n" +
    "    # Payments\n" +
    "    - amount_paid (decimal): ยอดที่จ่ายจริง (มากกว่า total_due)\n" +
    "    - principal_paid (decimal): เงินต้นที่ชำระ\n" +
    "    - interest_paid (decimal): ดอกเบี้ยที่ชำระ\n" +
    "    - penalty_paid (decimal): ค่าปรับที่จ่าย\n" +
    "\n" +
    "    - last_payment_date (date): วันที่มีการชำระครั้งล่าสุดของงวดนี้\n" +
    "    - overpay_amount (decimal): ยอดที่จ่ายเกิน (amount_paid - total_due)")]
    public static async Task<object> qry_prepayment_history(
        SqlRunner db,
        [Description("รหัสสัญญา (optional). ไม่ระบุ = ทั้งหมด")] int? loan_id = null)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryPrepaymentHistory,
            Params = P(("loan_id", loan_id))
        });

    // 15) Loan balance snapshot as-of date
    [McpServerTool, Description(
    "Purpose: ภาพรวมยอดคงเหลือเงินต้นของสัญญา ณ วันที่กำหนด (as-of) พร้อมสเตตัสสัญญาถึงวันดังกล่าว.\n" +
    "Params:\n" +
    "  loan_id (int, required) = รหัสสัญญา.\n" +
    "  as_of (DateTime, required, yyyy-MM-dd) = วันที่อ้างอิง (inclusive).\n" +
    "\n" +
    "Result: คืน 1 row แสดงภาพรวมสัญญา ณ วันที่ as-of ในโครงสร้าง:\n" +
    "  [loan_id, contract_number, loan_amount,\n" +
    "   principal_paid_asof, principal_balance_asof,\n" +
    "   start_date, end_date, status, as_of_date]\n" +
    "\n" +
    "    - loan_id (int): รหัสสัญญาเงินกู้\n" +
    "    - contract_number (string): เลขที่สัญญา เช่น LO20250105-0002\n" +
    "    - loan_amount (decimal): วงเงินกู้ตั้งต้น\n" +
    "\n" +
    "    - principal_paid_asof (decimal): เงินต้นที่จ่ายสะสมจนถึงวันที่ as-of\n" +
    "    - principal_balance_asof (decimal): เงินต้นคงเหลือตามการคำนวณ ณ as-of\n" +
    "\n" +
    "    - start_date (date): วันที่เริ่มสัญญา\n" +
    "    - end_date (date): วันที่สิ้นสุดสัญญาตามแผน\n" +
    "    - status (string): สถานะสัญญา ณ ปัจจุบัน เช่น ACTIVE, Closed\n" +
    "    - as_of_date (date?): วันที่อ้างอิงที่ใช้คำนวณ (โดยทั่วไปตรงกับพารามิเตอร์ as_of; อาจเป็น null ในบางกรณี)")]
    public static async Task<object> qry_loan_balance_asof(
        SqlRunner db,
        [Description("รหัสสัญญา (required). ตัวอย่าง: 1")] int loan_id,
        [Description("วันที่อ้างอิงรูปแบบ yyyy-MM-dd (required). ตัวอย่าง: 2025-06-30")] DateTime as_of)
        => await db.RunProcedureAsync(new RunProcedureRequest
        {
            ProcedureName = Proc.QryLoanBalanceAsOf,
            Params = P(("loan_id", loan_id), ("as_of", as_of))
        });
}
