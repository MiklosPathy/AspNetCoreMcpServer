using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class FileSearchTool
{
    private static string ResolvePath(string path)
    {
        // If the path is already absolute, use it as-is
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        // For relative paths, resolve from the current working directory
        return Path.GetFullPath(path);
    }

    [McpServerTool(Name = "search_files"), Description("Search files by name pattern.")]
    public string[] SearchFiles(
        [Description("Directory to search in (relative or absolute path)")] string directory,
        [Description("Search pattern, e.g. *.cs, Program*.cs")] string pattern,
        [Description("Search subdirectories (default: true)")] bool recursive = true)
    {
        var full = ResolvePath(directory);
        if (!Directory.Exists(full))
            throw new McpException($"Directory not found: {directory}");

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.GetFiles(full, pattern, option);
    }
}