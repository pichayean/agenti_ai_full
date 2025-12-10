namespace MssqlMcpServer.Infrastructure; 
public sealed class DatabaseOptions{ public string ConnectionString{get;set;}= ""; public int CommandTimeoutSeconds{get;set;}=30;}