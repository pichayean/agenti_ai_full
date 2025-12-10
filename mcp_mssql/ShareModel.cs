
namespace MssqlMcpServer;

public sealed class RunSelectRequest
{
    public string Sql { get; set; } = "";
    public Dictionary<string, object>? Params { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class RunProcedureRequest
{
    public string ProcedureName { get; set; } = "";
    public Dictionary<string, object>? Params { get; set; }
}

public sealed class DescribeRequest
{
    public bool? IncludeDefinition { get; set; }
}
