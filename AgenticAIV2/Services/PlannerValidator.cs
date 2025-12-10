using System.Text.Json;

namespace AgenticAI.Services;
public class PlannerValidator
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

    public static void Validate(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // check version
        if (root.GetProperty("Version").GetString() != "1.0")
            throw new Exception("Version must be '1.0'");

        // check constraints
        var constraints = root.GetProperty("Constraints");
        if (constraints.GetProperty("MaxSteps").GetInt32() > 8)
            throw new Exception("MaxSteps > 8 not allowed");
        if (constraints.GetProperty("TimeoutSec").GetInt32() != 120)
            throw new Exception("TimeoutSec must be 120");

        // check steps
        var steps = root.GetProperty("Steps").EnumerateArray().ToList();
        if (steps.Count > 8)
            throw new Exception("Too many steps (max 8)");

        foreach (var s in steps)
        {
            string id = s.GetProperty("Id").GetString() ?? throw new Exception("Step Id missing");
            string type = s.GetProperty("Type").GetString() ?? "";
            string plugin = s.GetProperty("Plugin").ValueKind == JsonValueKind.Null ? null : s.GetProperty("Plugin").GetString();
            string tool = s.GetProperty("Tool").ValueKind == JsonValueKind.Null ? null : s.GetProperty("Tool").GetString();

            if (type == "tool")
            {
                if (string.IsNullOrEmpty(plugin) || string.IsNullOrEmpty(tool))
                    throw new Exception($"Step '{id}' must have Plugin and Tool for Type=tool");

                string key = $"{plugin}.{tool}";
                if (!AllowedTools.Contains(key))
                    throw new Exception($"Tool not allowed: {key}");
            }
            else if (type == "llm")
            {
                if (plugin != null || tool != null)
                    throw new Exception($"Step '{id}' of type 'llm' must not have Plugin/Tool");
            }
            else
            {
                throw new Exception($"Invalid Type in step '{id}'");
            }
        }
    }
}