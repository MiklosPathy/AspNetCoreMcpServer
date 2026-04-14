using HtmlAgilityPack;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class WebSearchTool(IHttpClientFactory httpClientFactory)
{
    [McpServerTool(Name = "web_search"), Description("""
        Search the web using DuckDuckGo.

        CRITICAL: You are forbidden from answering questions about current events, news,
        space missions, or any factual data that could have changed since your last
        training update without using this tool.

        TRIGGER RULE: You MUST call this tool immediately if the user's query contains
        any temporal indicators: 'latest', 'current', 'recent', 'news', 'status',
        'update', 'now', 'today', 'this week', or if it refers to an ongoing event,
        mission, or process.

        Do not rely on your internal knowledge for time-sensitive topics; always
        verify with a live search to avoid hallucination.
        """)]
    public async Task<string> WebSearch(
        [Description("The search query string")] string query,
        [Description("Number of results to return (default: 20)")] int maxResults = 20)
    {
        var client = httpClientFactory.CreateClient("DuckDuckGo");

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["q"] = query,
            ["kl"] = "wt-wt",
        });

        var response = await client.PostAsync("https://html.duckduckgo.com/html/", formData);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<(string title, string body, string href)>();

        var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class,'result__body')]");
        if (resultNodes != null)
        {
            foreach (var node in resultNodes)
            {
                if (results.Count >= maxResults) break;

                var titleNode = node.SelectSingleNode(".//a[contains(@class,'result__a')]");
                var snippetNode = node.SelectSingleNode(".//*[contains(@class,'result__snippet')]");

                if (titleNode == null) continue;

                var title = HtmlEntity.DeEntitize(titleNode.InnerText.Trim());
                var body = snippetNode != null ? HtmlEntity.DeEntitize(snippetNode.InnerText.Trim()) : "";
                var href = titleNode.GetAttributeValue("href", "");

                href = ExtractUrl(href);
                results.Add((title, body, href));
            }
        }

        if (results.Count == 0)
            return $"Nem találtam eredményt erre: '{query}'. Próbálj más kulcsszavakkal.";

        var lines = results.Select((r, i) => $"{i + 1}. {r.title}\n   {r.body}\n   {r.href}");
        return string.Join("\n\n", lines);
    }

    private static string ExtractUrl(string href)
    {
        if (string.IsNullOrEmpty(href)) return href;

        // DDG redirect URL: //duckduckgo.com/l/?uddg=<encoded-url>&...
        if (href.Contains("uddg="))
        {
            var start = href.IndexOf("uddg=") + 5;
            var end = href.IndexOf('&', start);
            var encoded = end >= 0 ? href[start..end] : href[start..];
            return Uri.UnescapeDataString(encoded);
        }

        if (href.StartsWith("//"))
            return "https:" + href;

        return href;
    }
}
