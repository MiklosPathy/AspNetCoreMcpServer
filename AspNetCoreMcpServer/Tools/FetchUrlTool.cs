using HtmlAgilityPack;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class FetchUrlTool(IHttpClientFactory httpClientFactory)
{
    private static readonly HashSet<string> HtmlContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/html", "application/xhtml+xml", "application/xhtml"
    };

    private static readonly HashSet<string> TextContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain", "text/csv", "text/markdown", "text/xml",
        "application/json", "application/xml", "application/rss+xml", "application/atom+xml",
        "application/javascript", "application/typescript",
        "text/css", "text/javascript",
        "application/yaml", "application/x-yaml", "text/yaml",
        "text/turtle", "application/ld+json",
    };

    private static readonly HashSet<string> BinaryContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "application/zip", "application/gzip",
        "application/octet-stream", "application/x-tar",
        "image/", "audio/", "video/",
    };

    private static readonly string[] NoiseTags =
    [
        "script", "style", "noscript", "nav", "header", "footer",
        "aside", "form", "iframe", "svg", "button", "input", "select", "textarea",
    ];

    private static readonly string[] NoiseRoles =
    [
        "navigation", "banner", "contentinfo", "complementary",
        "search", "dialog", "alertdialog", "alert",
    ];

    private static readonly string[] NoiseClassesOrIds =
    [
        "sidebar", "menu", "nav", "footer", "header", "banner",
        "cookie", "popup", "modal", "overlay", "ad", "ads", "advert",
        "social", "share", "comment", "comments", "related", "recommendation",
        "newsletter", "subscribe", "paywall", "promo", "sponsor",
        "breadcrumb", "pagination", "pager",
    ];

    [McpServerTool(Name = "fetch_url"), Description("""
        Fetch and extract the readable text content of a specific URL.

        WORKFLOW RULE: Use this tool IMMEDIATELY after a web_search if the search
        snippets are insufficient to provide a complete answer, or when a user
        provides a specific link to analyze, summarize, or extract data from.
        This is your primary way to read deep content beyond search snippets.

        Features:
        - Handles HTML, JSON, XML, RSS/Atom, plain text, and other text-based content types
        - Smart HTML content extraction: removes noise (ads, nav, footers, sidebars, etc.)
        - Extracts metadata: title, description, author, published date, OpenGraph tags
        - Extracts links from the page
        - Supports GET and POST methods with custom headers and body
        - Automatic decompression (gzip, brotli, deflate)
        - Cookie-aware: handles session cookies across redirects
        - Smart truncation at paragraph/sentence boundaries
        - Retry logic with exponential backoff
        """)]
    public async Task<string> FetchUrl(
        [Description("The URL to fetch")] string url,
        [Description("Maximum characters to return (default: 8000)")] int maxChars = 8000,
        [Description("HTTP method: GET or POST (default: GET)")] string method = "GET",
        [Description("Request body for POST requests (optional)")] string? body = null,
        [Description("Content-Type for the request body, e.g. application/json (optional)")] string? contentType = null,
        [Description("Comma-separated custom headers in 'Name: Value' format (optional)")] string? customHeaders = null,
        [Description("Include extracted links in the output (default: true)")] bool includeLinks = true,
        [Description("Include metadata (description, author, og tags) in the output (default: true)")] bool includeMetadata = true,
        [Description("Maximum number of links to include (default: 30)")] int maxLinks = 30)
    {
        // ── Validate URL ──────────────────────────────────────────────
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return $"Hibás URL: '{url}'. Csak http/https sémák támogatottak.";
        }

        // ── Build request ─────────────────────────────────────────────
        var client = httpClientFactory.CreateClient("FetchUrl");
        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), uri);

        // Custom headers
        if (!string.IsNullOrWhiteSpace(customHeaders))
        {
            foreach (var headerLine in customHeaders.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var sepIdx = headerLine.IndexOf(':');
                if (sepIdx > 0)
                {
                    var name = headerLine[..sepIdx].Trim();
                    var value = headerLine[(sepIdx + 1)..].Trim();
                    request.Headers.TryAddWithoutValidation(name, value);
                }
            }
        }

        // Body
        if (!string.IsNullOrWhiteSpace(body) && request.Method == HttpMethod.Post)
        {
            var mediaType = contentType ?? "application/octet-stream";
            request.Content = new StringContent(body, Encoding.UTF8, mediaType);
        }

        // ── Send with retry ───────────────────────────────────────────
        HttpResponseMessage? response = null;
        const int maxRetries = 3;
        Exception? lastException = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * (1 << (attempt - 1)))); // exponential backoff

                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                // Retry on transient server errors
                if ((int)response.StatusCode >= 500 && attempt < maxRetries)
                {
                    response.Dispose();
                    continue;
                }

                response.EnsureSuccessStatusCode();
                break;
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue && (int)ex.StatusCode >= 500 && attempt < maxRetries)
            {
                lastException = ex;
                continue;
            }
            catch (HttpRequestException ex) when (ex.StatusCode.HasValue)
            {
                var statusCode = (int)ex.StatusCode.Value;
                var reason = ex.StatusCode.Value.ToString();
                return $"HTTP {statusCode} ({reason}) – {url}\nA szerver visszautasította a kérést.";
            }
            catch (TaskCanceledException)
            {
                return $"Időtúllépés – {url}\nA szerver nem válaszolt a megadott időn belül.";
            }
            catch (Exception ex)
            {
                return $"Nem sikerült elérni az oldalt: {ex.Message}";
            }
        }

        if (response is null)
            return $"Nem sikerült elérni az oldalt {maxRetries + 1} próbálkozás után. Utolsó hiba: {lastException?.Message}";

        using var _ = response;

        // ── Response metadata ─────────────────────────────────────────
        var responseContentType = response.Content.Headers.ContentType?.MediaType ?? "";
        var contentLength = response.Content.Headers.ContentLength;
        var effectiveUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;

        // ── Read body ─────────────────────────────────────────────────
        string bodyText;
        try
        {
            bodyText = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"Nem sikerült beolvasni a választ: {ex.Message}";
        }

        // ── Binary content detection ──────────────────────────────────
        if (IsBinaryContentType(responseContentType))
        {
            return BuildBinaryResponse(responseContentType, contentLength, effectiveUrl, response);
        }

        // ── Non-HTML text content ─────────────────────────────────────
        if (!HtmlContentTypes.Contains(responseContentType))
        {
            var cleaned = bodyText;
            // Pretty-print JSON if possible
            if (responseContentType.Contains("json") && TryFormatJson(bodyText, out var formatted))
                cleaned = formatted;

            var header = BuildResponseHeader(effectiveUrl, response.StatusCode, responseContentType, contentLength);
            var result = $"{header}\n\n{cleaned}";

            if (result.Length > maxChars)
                result = SmartTruncate(result, maxChars);

            return result;
        }

        // ── HTML content ──────────────────────────────────────────────
        return ProcessHtmlContent(bodyText, effectiveUrl, response.StatusCode, responseContentType, contentLength, maxChars, includeLinks, includeMetadata, maxLinks);
    }

    // ══════════════════════════════════════════════════════════════════
    //  HTML Processing
    // ══════════════════════════════════════════════════════════════════

    private static string ProcessHtmlContent(
        string html, string effectiveUrl, HttpStatusCode statusCode,
        string contentType, long? contentLength, int maxChars,
        bool includeLinks, bool includeMetadata, int maxLinks)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // ── Extract metadata first (before removing nodes) ───────────
        var metadata = includeMetadata ? ExtractMetadata(doc) : null;

        // ── Extract links before removing nodes ───────────────────────
        var links = includeLinks ? ExtractLinks(doc, effectiveUrl, maxLinks) : null;

        // ── Remove noise ──────────────────────────────────────────────
        RemoveNoiseNodes(doc);

        // ── Extract title ─────────────────────────────────────────────
        var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText.Trim() ?? "";
        title = HtmlEntity.DeEntitize(title).Replace('\n', ' ').Trim();

        // ── Find main content area ────────────────────────────────────
        var main = FindMainContent(doc);

        // ── Extract and clean text ────────────────────────────────────
        var rawText = ExtractTextFromNode(main ?? doc.DocumentNode);
        var clean = HtmlEntity.DeEntitize(rawText);

        // ── Build output ──────────────────────────────────────────────
        var sb = new StringBuilder();

        // Response header
        sb.AppendLine(BuildResponseHeader(effectiveUrl, statusCode, contentType, contentLength));

        // Title
        if (!string.IsNullOrEmpty(title))
            sb.AppendLine($"\n# {title}");

        // Metadata
        if (metadata is not null && metadata.Count > 0)
        {
            sb.AppendLine();
            foreach (var (key, value) in metadata)
                sb.AppendLine($"**{key}**: {value}");
        }

        // Main content
        sb.AppendLine($"\n---\n");
        sb.Append(clean);

        // Links
        if (links is not null && links.Count > 0)
        {
            sb.AppendLine($"\n---\n## Links ({links.Count})\n");
            foreach (var (text, href) in links)
                sb.AppendLine($"- [{text}]({href})");
        }

        var result = sb.ToString();

        if (result.Length > maxChars)
            result = SmartTruncate(result, maxChars);

        return result;
    }

    // ══════════════════════════════════════════════════════════════════
    //  Metadata Extraction
    // ══════════════════════════════════════════════════════════════════

    private static List<(string Key, string Value)> ExtractMetadata(HtmlDocument doc)
    {
        var meta = new List<(string, string)>();

        // Standard meta tags
        AddMeta(doc, "description", "Description", meta);
        AddMeta(doc, "author", "Author", meta);
        AddMeta(doc, "date", "Published", meta);
        AddMeta(doc, "article:published_time", "Published", meta);
        AddMeta(doc, "article:modified_time", "Modified", meta);
        AddMeta(doc, "keywords", "Keywords", meta);

        // OpenGraph
        AddMeta(doc, "og:title", "OG Title", meta);
        AddMeta(doc, "og:description", "OG Description", meta);
        AddMeta(doc, "og:type", "OG Type", meta);
        AddMeta(doc, "og:image", "OG Image", meta);
        AddMeta(doc, "og:site_name", "OG Site", meta);

        // Twitter
        AddMeta(doc, "twitter:card", "Twitter Card", meta);
        AddMeta(doc, "twitter:title", "Twitter Title", meta);
        AddMeta(doc, "twitter:description", "Twitter Description", meta);
        AddMeta(doc, "twitter:image", "Twitter Image", meta);

        // Canonical URL
        var canonical = doc.DocumentNode.SelectSingleNode("//link[@rel='canonical']/@href");
        if (canonical is not null)
            meta.Add(("Canonical URL", canonical.GetAttributeValue("href", "")));

        // Lang
        var lang = doc.DocumentNode.SelectSingleNode("//html")?.GetAttributeValue("lang", "");
        if (!string.IsNullOrEmpty(lang))
            meta.Add(("Language", lang));

        return meta;
    }

    private static void AddMeta(HtmlDocument doc, string nameOrProperty, string label, List<(string, string)> meta)
    {
        // Try name attribute first, then property attribute
        var node = doc.DocumentNode.SelectSingleNode($"//meta[@name='{nameOrProperty}']")
                   ?? doc.DocumentNode.SelectSingleNode($"//meta[@property='{nameOrProperty}']");
        if (node is not null)
        {
            var content = node.GetAttributeValue("content", "").Trim();
            if (!string.IsNullOrEmpty(content))
                meta.Add((label, content));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Link Extraction
    // ══════════════════════════════════════════════════════════════════

    private static List<(string Text, string Href)> ExtractLinks(HtmlDocument doc, string baseUrl, int maxLinks)
    {
        var links = new List<(string, string)>();
        var baseUri = new Uri(baseUrl);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var anchorNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes is null) return links;

        foreach (var anchor in anchorNodes)
        {
            if (links.Count >= maxLinks) break;

            var href = anchor.GetAttributeValue("href", "").Trim();
            if (string.IsNullOrEmpty(href)) continue;

            // Skip anchors, javascript, mailto
            if (href.StartsWith('#') || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Resolve relative URLs
            try
            {
                var resolved = new Uri(baseUri, href);
                href = resolved.ToString();
            }
            catch
            {
                continue;
            }

            // Deduplicate
            if (!seen.Add(href)) continue;

            var text = HtmlEntity.DeEntitize(anchor.InnerText.Trim()).Replace('\n', ' ').Trim();
            if (string.IsNullOrEmpty(text))
                text = href;

            // Truncate long link text
            if (text.Length > 120)
                text = text[..117] + "...";

            links.Add((text, href));
        }

        return links;
    }

    // ══════════════════════════════════════════════════════════════════
    //  Noise Removal
    // ══════════════════════════════════════════════════════════════════

    private static void RemoveNoiseNodes(HtmlDocument doc)
    {
        // Remove by tag name
        foreach (var tag in NoiseTags)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{tag}");
            if (nodes is not null)
                foreach (var node in nodes.ToList())
                    node.Remove();
        }

        // Remove by role attribute
        foreach (var role in NoiseRoles)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//*[@role='{role}']");
            if (nodes is not null)
                foreach (var node in nodes.ToList())
                    node.Remove();
        }

        // Remove by class/id containing noise keywords
        foreach (var keyword in NoiseClassesOrIds)
        {
            var xpath = $"//*[contains(concat(' ',normalize-space(@class),' '),' {keyword} ') or contains(concat(' ',normalize-space(@id),' '),' {keyword} ')]";
            var nodes = doc.DocumentNode.SelectNodes(xpath);
            if (nodes is not null)
                foreach (var node in nodes.ToList())
                    node.Remove();
        }

        // Remove hidden elements
        var hiddenNodes = doc.DocumentNode.SelectNodes(
            "//*[@hidden or contains(@style,'display:none') or contains(@style,'display: none') or contains(@style,'visibility:hidden') or contains(@style,'visibility: hidden')]");
        if (hiddenNodes is not null)
            foreach (var node in hiddenNodes.ToList())
                node.Remove();
    }

    // ══════════════════════════════════════════════════════════════════
    //  Main Content Detection
    // ══════════════════════════════════════════════════════════════════

    private static HtmlNode? FindMainContent(HtmlDocument doc)
    {
        // Priority order for content containers
        var candidates = new[]
        {
            "//main",
            "//article",
            "//div[@role='main']",
            "//div[@id='content']",
            "//div[@id='main-content']",
            "//div[@id='main']",
            "//div[@class='content']",
            "//div[@class='main-content']",
            "//div[@class='post-content']",
            "//div[@class='article-content']",
            "//div[@class='entry-content']",
            "//div[@class='post-body']",
            "//div[contains(@class, 'content') and not(contains(@class, 'sidebar'))]",
        };

        foreach (var xpath in candidates)
        {
            var node = doc.DocumentNode.SelectSingleNode(xpath);
            if (node is not null && node.InnerText.Length > 200)
                return node;
        }

        // Fallback: body
        return doc.DocumentNode.SelectSingleNode("//body");
    }

    // ══════════════════════════════════════════════════════════════════
    //  Text Extraction
    // ══════════════════════════════════════════════════════════════════

    private static string ExtractTextFromNode(HtmlNode node)
    {
        var sb = new StringBuilder();
        ExtractTextRecursive(node, sb, 0);
        return sb.ToString();
    }

    private static void ExtractTextRecursive(HtmlNode node, StringBuilder sb, int depth)
    {
        if (depth > 50) return; // prevent deep recursion

        switch (node.NodeType)
        {
            case HtmlNodeType.Text:
                var text = node.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Collapse whitespace within text nodes
                    var normalized = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
                    sb.Append(normalized);
                }
                break;

            case HtmlNodeType.Element:
                var tag = node.Name.ToLowerInvariant();

                // Block-level elements → add newlines
                if (IsBlockElement(tag))
                {
                    sb.AppendLine();
                    foreach (var child in node.ChildNodes)
                        ExtractTextRecursive(child, sb, depth + 1);
                    sb.AppendLine();
                }
                // Headings → add markdown-style prefix
                else if (tag is "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
                {
                    var level = tag[1] - '0';
                    sb.AppendLine();
                    sb.Append($"{new string('#', level)} ");
                    foreach (var child in node.ChildNodes)
                        ExtractTextRecursive(child, sb, depth + 1);
                    sb.AppendLine();
                }
                // List items
                else if (tag == "li")
                {
                    sb.Append("- ");
                    foreach (var child in node.ChildNodes)
                        ExtractTextRecursive(child, sb, depth + 1);
                    sb.AppendLine();
                }
                // Line breaks
                else if (tag is "br" or "hr")
                {
                    sb.AppendLine();
                }
                // Inline elements → just recurse
                else
                {
                    foreach (var child in node.ChildNodes)
                        ExtractTextRecursive(child, sb, depth + 1);
                }
                break;
        }
    }

    private static bool IsBlockElement(string tag) =>
        tag is "p" or "div" or "section" or "blockquote" or "pre" or "table"
            or "tr" or "td" or "th" or "dl" or "dt" or "dd" or "ul" or "ol"
            or "figure" or "figcaption" or "details" or "summary" or "address";

    // ══════════════════════════════════════════════════════════════════
    //  Smart Truncation
    // ══════════════════════════════════════════════════════════════════

    private static string SmartTruncate(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;

        // Try to truncate at a paragraph boundary
        var cutOff = text[..maxChars];
        var lastParagraphBreak = cutOff.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (lastParagraphBreak > maxChars * 0.5)
            return text[..lastParagraphBreak] + $"\n\n[...tartalom csonkítva, {text.Length:N0} karakterből {lastParagraphBreak:N0} látható]";

        // Try to truncate at a sentence boundary
        var lastSentenceEnd = cutOff.LastIndexOfAny(['.', '!', '?', '。']);
        if (lastSentenceEnd > maxChars * 0.5)
            return text[..(lastSentenceEnd + 1)] + $"\n\n[...tartalom csonkítva, {text.Length:N0} karakterből {lastSentenceEnd + 1:N0} látható]";

        // Try to truncate at a newline
        var lastNewline = cutOff.LastIndexOf('\n');
        if (lastNewline > maxChars * 0.5)
            return text[..lastNewline] + $"\n\n[...tartalom csonkítva, {text.Length:N0} karakterből {lastNewline:N0} látható]";

        // Last resort: hard cut at word boundary
        var lastSpace = cutOff.LastIndexOf(' ');
        if (lastSpace > maxChars * 0.5)
            return text[..lastSpace] + $"\n\n[...tartalom csonkítva, {text.Length:N0} karakterből {lastSpace:N0} látható]";

        return cutOff + $"\n\n[...tartalom csonkítva, {text.Length:N0} karakterből {maxChars:N0} látható]";
    }

    // ══════════════════════════════════════════════════════════════════
    //  Helpers
    // ══════════════════════════════════════════════════════════════════

    private static bool IsBinaryContentType(string contentType)
    {
        foreach (var binaryPrefix in BinaryContentTypes)
        {
            if (contentType.StartsWith(binaryPrefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string BuildResponseHeader(string url, HttpStatusCode statusCode, string contentType, long? contentLength)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**URL**: {url}");
        sb.AppendLine($"**Status**: {(int)statusCode} {statusCode}");
        sb.AppendLine($"**Content-Type**: {contentType}");
        if (contentLength.HasValue)
            sb.AppendLine($"**Content-Length**: {contentLength.Value:N0} bytes");
        return sb.ToString().TrimEnd();
    }

    private static string BuildBinaryResponse(string contentType, long? contentLength, string url, HttpResponseMessage response)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**URL**: {url}");
        sb.AppendLine($"**Status**: {(int)response.StatusCode} {response.StatusCode}");
        sb.AppendLine($"**Content-Type**: {contentType}");
        if (contentLength.HasValue)
            sb.AppendLine($"**Content-Length**: {contentLength.Value:N0} bytes");
        sb.AppendLine();
        sb.AppendLine("⚠️ Bináris tartalom – nem jeleníthető meg szövegként.");
        sb.AppendLine($"Típus: {contentType}");
        return sb.ToString();
    }

    private static bool TryFormatJson(string raw, out string formatted)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(raw);
            formatted = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            return true;
        }
        catch
        {
            formatted = "";
            return false;
        }
    }
}