using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace AspNetCoreMcpServer.Tools;

[McpServerToolType]
public class FileSystemTool
{
    private static string ResolvePath(string path)
    {
        // If the path is already absolute, use it as-is
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        // For relative paths, resolve from the current working directory
        return Path.GetFullPath(path);
    }

    [McpServerTool(Name = "file_read"), Description("Read the contents of a text file.")]
    public string FileRead(
        [Description("Relative or absolute file path")] string path)
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
            .Select(f => Path.GetFileName(f))
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