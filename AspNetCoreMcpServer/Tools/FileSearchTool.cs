using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class FileSearchTool
{
    private readonly string _basePath;

    public FileSearchTool()
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

    [McpServerTool(Name = "search_files"), Description("Search files by name pattern.")]
    public string[] SearchFiles(
        [Description("Directory to search in (relative to solution root)")] string directory,
        [Description("Search pattern, e.g. *.cs, Program*.cs")] string pattern,
        [Description("Search subdirectories (default: true)")] bool recursive = true)
    {
        var full = ResolvePath(directory);
        if (!Directory.Exists(full))
            throw new McpException($"Directory not found: {directory}");

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(full, pattern, option)
            .Select(f => Path.GetRelativePath(_basePath, f))
            .ToArray();
    }
}
