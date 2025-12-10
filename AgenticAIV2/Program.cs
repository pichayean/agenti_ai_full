using AgenticAI.Evaluation;
using AgenticAI.Infrastructure;
using AgenticAI.Models;
using AgenticAI.Services;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAddMCPServer(builder.Configuration);
builder.Services.AddModelAiProviderKernel(builder.Configuration);

builder.Services.Configure<ExecutionPolicy>(builder.Configuration.GetSection("ExecutionPolicy"));

builder.Services.AddSingleton<PlannerService>();
builder.Services.AddSingleton<ValidatorService>();
builder.Services.AddSingleton<ToolsExecutor>();
builder.Services.AddSingleton<ComposerService>();
builder.Services.AddSingleton(new RetentionStore(TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("RetentionMinutes", 30))));
builder.Services.AddHttpClient("agent");
builder.Services.AddSingleton<EvalRunner>();
builder.Services.AddSingleton<OrchestratorService>();

//builder.Services.AddSingleton<ILLMCounterStore, LLMCounterStore>();
builder.Services.AddHttpContextAccessor();

// Singleton ก็ได้ เพราะมันไม่ถือ state เอง
builder.Services.AddSingleton<ILLMCounterAccessor, HttpContextLLMCounterAccessor>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "AgenticAI", Version = "v1" }));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.UseStaticFiles();  // ให้เสิร์ฟไฟล์ static จาก wwwroot
// หน้าเว็บหลัก
app.MapGet("/agentic/chat", context =>
{
    context.Response.Redirect("/agentic/index.html");
    return Task.CompletedTask;
});

app.MapGet("/report", context =>
{
    context.Response.Redirect("/agentic/report.html");
    return Task.CompletedTask;
});
app.MapGet("/style.css", context =>
{
    context.Response.Redirect("/agentic/style.css");
    return Task.CompletedTask;
});

app.MapGet("/app.js", context =>
{
    context.Response.Redirect("/agentic/app.js");
    return Task.CompletedTask;
});


app.MapGet("/", context =>
{
    context.Response.Redirect("/index.html");
    return Task.CompletedTask;
});

app.Run();
