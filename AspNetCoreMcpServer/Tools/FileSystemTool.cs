using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class FileSystemTool
{
    private readonly string _basePath;

    public FileSystemTool()
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

    [McpServerTool(Name = "file_read"), Description("Read the contents of a text file.")]
    public string FileRead(
        [Description("Relative or absolute file path from solution root")] string path)
    {
        var full = ResolvePath(path);
        if (!File.Exists(full))
            throw new McpException($"File not found: {path}");
        return File.ReadAllText(full);
    }

    [McpServerTool(Name = "file_write"), Description("Write text to a file. Creates directories if needed.")]
    public void FileWrite(
        [Description("File path")] string path,
        [Description("Content to write")] string content)
    {
        var full = ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [McpServerTool(Name = "directory_list"), Description("List files and subdirectories.")]
    public string[] DirectoryList(
        [Description("Directory path")] string path)
    {
        var full = ResolvePath(path);
        if (!Directory.Exists(full))
            throw new McpException($"Directory not found: {path}");

        return Directory.GetFileSystemEntries(full)
            .Select(f => Path.GetRelativePath(_basePath, f))
            .ToArray();
    }

    [McpServerTool(Name = "file_exists"), Description("Check if a file or directory exists.")]
    public bool Exists(
        [Description("Path to check")] string path)
    {
        var full = ResolvePath(path);
        return File.Exists(full) || Directory.Exists(full);
    }
}
