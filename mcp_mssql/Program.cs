using System.Text.Json;
using ModelContextProtocol.Server;
using MssqlMcpServer;
using MssqlMcpServer.Common;
using MssqlMcpServer.Infrastructure;
using MssqlMcpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

var dbCfg = builder.Configuration.GetSection("MssqlMcpServer:Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
builder.Services.AddSingleton(dbCfg);
builder.Services.AddSingleton<SqlRunner>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", async (SqlRunner db, DatabaseOptions options) =>
{
    var conn = ConnectionStringMasker.MaskSensitive(options.ConnectionString ?? "");
    var connmessage = "n/a";
    try
    {
        connmessage = JsonSerializer.Serialize(await db.CheckConnectivityAsync());
    }
    catch (Exception ex)
    {
        connmessage = ex.Message;
    }

    return Results.Ok(new
    {
        Status = "Healthy",
        DatabaseConnectionString = conn,
        DatabaseConnected = connmessage
    });
});

app.MapGet("/", () => new { status = "ok", service = "MSSQL MCP Server", link = "/health" });
//// keep REST shim for debug
//app.MapPost("/tools/check_connectivity", async (SqlRunner db) => Results.Ok(await db.CheckConnectivityAsync()));
//app.MapPost("/tools/run_select", async (RunSelectRequest req, SqlRunner db) => Results.Ok(await db.RunSelectAsync(req)));
//app.MapPost("/tools/describe_database", async (DescribeRequest req, SqlRunner db) => Results.Ok(await db.DescribeDatabaseAsync(req)));
//app.MapPost("/tools/list_procedures", async (DescribeRequest req, SqlRunner db) => Results.Ok(await db.ListProceduresAsync(req)));

// SSE endpoint for MCP spec
app.MapMcp("/mcp");

app.Run();
