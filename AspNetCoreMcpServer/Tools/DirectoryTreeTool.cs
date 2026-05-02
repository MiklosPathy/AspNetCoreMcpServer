using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class DirectoryTreeTool
{
    private readonly string _basePath;

    public DirectoryTreeTool()
    {
        _basePath = FindSolutionRoot();
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            if (dir.EnumerateFiles("*.slnx").Any() || dir.EnumerateFiles("*.sln").Any())
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private string ResolvePath(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_basePath, path));
        if (!full.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
            throw new McpException($"Path traversal denied: {path}");
        return full;
    }

    [McpServerTool(Name = "get_directory_tree"), Description("Get recursive directory tree as formatted text.")]
    public string GetTree(
        [Description("Directory path relative to solution root")] string path,
        [Description("Max depth (default: 3)")] int maxDepth = 3,
        [Description("Exclude patterns (e.g. bin,obj,.git)")] string[]? exclude = null)
    {
        exclude ??= new[] { "bin", "obj", ".git", "node_modules", ".vs" };
        var full = ResolvePath(path);
        var dir = new DirectoryInfo(full);
        if (!dir.Exists)
            throw new McpException($"Directory not found: {path}");

        var sb = new StringBuilder();
        BuildTree(dir, "", 0, maxDepth, exclude, sb);
        return sb.ToString();
    }

    private static void BuildTree(DirectoryInfo dir, string prefix, int depth, int maxDepth, string[] exclude, StringBuilder sb)
    {
        if (depth > maxDepth) return;
        if (exclude.Contains(dir.Name, StringComparer.OrdinalIgnoreCase)) return;

        sb.AppendLine(prefix + dir.Name + "/");

        foreach (var file in dir.GetFiles().Take(30))
            sb.AppendLine(prefix + "  " + file.Name);

        foreach (var sub in dir.GetDirectories()
            .Where(d => !exclude.Contains(d.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(d => d.Name))
        {
            BuildTree(sub, prefix + "  ", depth + 1, maxDepth, exclude, sb);
        }
    }
}
