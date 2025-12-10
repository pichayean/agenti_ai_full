using AgenticAI.Infrastructure;
using AgenticAI.Models;

namespace AgenticAI.Services;

public class ValidatorService
{
    private static readonly HashSet<string> AllowedTools = new()
    {
        "mssql_mcp.qry_payments_by_date",
        "mssql_mcp.qry_loan_balance_asof",
        "mssql_mcp.qry_prepayment_history",
        "mssql_mcp.qry_payments_by_loan",
        "mssql_mcp.qry_upcoming_due",
        "mssql_mcp.qry_loan_overview",
        "mssql_mcp.qry_payment_schedule_by_loan",
        "mssql_mcp.qry_delinquency_aging",
        "mssql_mcp.qry_loans",
        "mssql_mcp.qry_interest_methods",
        "mssql_mcp.qry_customer_with_loans",
        "mssql_mcp.qry_collector_queue",
        "mssql_mcp.qry_loan_applications",
        "mssql_mcp.qry_customers",
        "mssql_mcp.qry_product_portfolio_summary",
        "mail_mcp.send_email"
    };
    private readonly ToolsAvailable _tools;
    public ValidatorService(ToolsAvailable tools) => _tools = tools;

    public Task<ValidationResult> ValidateAsync(Plan plan, CancellationToken ct = default)
    {
        var res = new ValidationResult { IsValid = true, NormalizedPlan = plan };
        if (plan.Steps.Count == 0) { res.IsValid = false; res.Errors.Add("Plan ต้องมีอย่างน้อย 1 step"); }
        if (plan.Steps.Count > 8) { res.IsValid = false; res.Errors.Add("เกิน maxSteps=8"); }
        foreach (var s in plan.Steps)
        {
            if (s.Type == "tool")
            {
                if (string.IsNullOrEmpty(s.Plugin) || string.IsNullOrEmpty(s.Tool))
                { res.IsValid = false; res.Errors.Add($"step {s.Id}: plugin/tool ว่าง"); continue; }

                var exists = _tools.AllTools().Any(t => t.plugin == s.Plugin && t.tool.Name == s.Tool);
                if (!exists) { res.IsValid = false; res.Errors.Add($"step {s.Id}: ไม่พบ {s.Plugin}.{s.Tool}"); }


                if (!AllowedTools.Contains($"{s.Plugin}.{s.Tool}"))
                {
                    res.IsValid = false;
                    res.Errors.Add($"step {s.Id}: tool ไม่ได้รับอนุญาต {s.Plugin}.{s.Tool}");
                }
            }
            if (string.IsNullOrWhiteSpace(s.Id)) { res.IsValid = false; res.Errors.Add("step id ว่าง"); }
        }
        return Task.FromResult(res);
    }
}
