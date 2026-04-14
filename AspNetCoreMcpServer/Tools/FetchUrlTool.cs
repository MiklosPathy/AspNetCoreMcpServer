using HtmlAgilityPack;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class FetchUrlTool(IHttpClientFactory httpClientFactory)
{
    [McpServerTool(Name = "fetch_url"), Description("""
        Fetch and extract the readable text content of a specific URL.

        WORKFLOW RULE: Use this tool IMMEDIATELY after a web_search if the search
        snippets are insufficient to provide a complete answer, or when a user
        provides a specific link to analyze, summarize, or extract data from.
        This is your primary way to read deep content beyond search snippets.

        Note: JavaScript-heavy SPAs and paywalled/bot-protected sites may not work.
        """)]
    public async Task<string> FetchUrl(
        [Description("The URL to fetch")] string url,
        [Description("Maximum characters to return (default: 8000)")] int maxChars = 8000)
    {
        var client = httpClientFactory.CreateClient("FetchUrl");

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
        {
            return $"HTTP hiba: {(int)ex.StatusCode} – {url}";
        }
        catch (Exception ex)
        {
            return $"Nem sikerült elérni az oldalt: {ex.Message}";
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var bodyText = await response.Content.ReadAsStringAsync();

        if (!contentType.Contains("text/html") && !contentType.Contains("application/xhtml"))
            return bodyText.Length > maxChars ? bodyText[..maxChars] : bodyText;

        var doc = new HtmlDocument();
        doc.LoadHtml(bodyText);

        // Távolítsuk el a nem kívánt tageket
        var tagsToRemove = new[] { "script", "style", "nav", "header", "footer", "aside", "form", "iframe" };
        foreach (var tag in tagsToRemove)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes != null)
                foreach (var node in nodes)
                    node.Remove();
        }

        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        var title = titleNode?.InnerText.Trim() ?? "";
        title = HtmlEntity.DeEntitize(title);

        var main = doc.DocumentNode.SelectSingleNode("//main")
            ?? doc.DocumentNode.SelectSingleNode("//article")
            ?? doc.DocumentNode.SelectSingleNode("//*[@id='content']")
            ?? doc.DocumentNode.SelectSingleNode("//body");

        var rawText = main?.InnerText ?? doc.DocumentNode.InnerText;

        var lines = rawText
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l));
        var clean = string.Join("\n", lines);
        clean = HtmlEntity.DeEntitize(clean);

        var result = !string.IsNullOrEmpty(title) ? $"# {title}\n\n{clean}" : clean;

        if (result.Length > maxChars)
            result = result[..maxChars] + $"\n\n[...tartalom csonkítva, {result.Length} karakterből {maxChars} látható]";

        return result;
    }
}
