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
    // Realistic Chrome User-Agent to avoid bot detection
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.9));
    client.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("hu", 0.8));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html", 1.0));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xhtml+xml", 0.9));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml", 0.8));
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.5));
    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
    client.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
    client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 15,
    AutomaticDecompression = System.Net.DecompressionMethods.All,
    UseCookies = true,
    CookieContainer = new System.Net.CookieContainer(),
});

var app = builder.Build();

app.UseCors();
app.MapMcp();

app.Run();