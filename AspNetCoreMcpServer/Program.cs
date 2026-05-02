using AspNetCoreMcpServer.Resources;
using AspNetCoreMcpServer.Tools;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddMcpServer()
    .WithHttpTransport(o => { o.Stateless = true; })
    .WithTools<DateTimeTool>()
    .WithTools<WebSearchTool>()
    .WithTools<FetchUrlTool>()
    .WithTools<FileSystemTool>()
    .WithTools<ShellTool>()
    .WithTools<GitTool>()
    .WithTools<DirectoryTreeTool>()
    .WithTools<FileSearchTool>()
    .WithTools<ProcessListTool>()
    .WithTools<EnvironmentTool>()
    .WithTools<ExpressionEvaluatorTool>()
    .WithResources<SimpleResourceType>();

builder.Services.AddHttpClient("DuckDuckGo", client =>
{
    client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
});

builder.Services.AddHttpClient("FetchUrl", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));
    client.Timeout = TimeSpan.FromSeconds(15);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 10,
});

var app = builder.Build();

app.UseCors();
app.MapMcp();

app.Run();
